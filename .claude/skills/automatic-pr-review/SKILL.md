---
name: automatic-pr-review
description: Code review a pull request on GitHub with comments
allowed-tools: Bash(gh issue view:*), Bash(gh search:*), Bash(gh issue list:*), Bash(gh pr comment:*), Bash(gh pr diff:*), Bash(gh pr view:*), Bash(gh pr list:*), Bash(gh api:*), mcp__github_inline_comment__create_inline_comment
---

<!-- Forked from anthropics/claude-plugins-official plugins/code-review/commands/code-review.md @ fd30cee (Apache-2.0) -->

Provide a code review for the given pull request.

**Agent assumptions (applies to all agents and subagents):**
- All tools are functional and will work without error. Do not test tools or make exploratory calls. Make sure this is clear to every subagent that is launched.
- Only call a tool if it is required to complete the task. Every tool call should have a clear purpose.
- Run every subagent in the foreground (`run_in_background: false`) and wait for its result. This review runs headless: it ends when your turn ends, so background agents never report back. To run agents in parallel, launch several foreground agents in one message.

**Before step 1:**
- Record the current PR head commit (`gh pr view <PR> --json headRefOid`). This is the commit you are reviewing, use it for the marker in step 8 even if new commits arrive during the review.
- Look for a summary comment from a previous review: `gh api repos/<owner>/<repo>/issues/<PR>/comments --paginate`, the last comment with `user.type == "Bot"` whose body contains `<!-- claude-review-sha: <40 hex chars> -->`. Remember its `id`, step 8 updates this comment. If there is none, do a full review.

**Re-review mode:** if a previous summary comment was found, the PR was already reviewed at the commit `<sha>` in its marker. Do not review the whole PR again. Instead:
- The changes to review are only those between `<sha>` and the head commit. Get them with `gh api repos/<owner>/<repo>/compare/<sha>...<head>`. If that call fails or reports `"status": "diverged"` (e.g. after a rebase or force push), do a full review instead and say so in the summary.
- The previously found issues are the unchecked `- [ ]` items in the previous summary comment.
- In steps 1 to 4, "the changes" means only this delta, not the whole PR diff.
- In step 3, also launch one Opus recheck agent. It gets the list of previously found issues and checks each one against the code at the head commit: fixed, or still present. It does not look for anything else.
- In steps 6 and 7, only post inline comments for new issues. Never re-post a previously found issue, whether it is fixed or still present.

To do this, follow these steps precisely:

1. Launch a haiku agent to return a list of file paths (not their contents) for all relevant CLAUDE.md files including:
   - The root CLAUDE.md file, if it exists
   - Any CLAUDE.md files in directories containing files modified by the pull request

2. Launch a sonnet agent to view the pull request and return a summary of the changes

3. Launch 4 agents in parallel to independently review the changes. Each agent should return the list of issues, where each issue includes a description and the reason it was flagged (e.g. "CLAUDE.md adherence", "bug"). The agents should do the following:

   Agents 1 + 2: CLAUDE.md compliance sonnet agents
   Audit changes for CLAUDE.md compliance in parallel. Note: When evaluating CLAUDE.md compliance for a file, you should only consider CLAUDE.md files that share a file path with the file or parents.

   Agent 3: Opus bug agent (parallel subagent with agent 4)
   Scan for obvious bugs. Focus only on the diff itself without reading extra context. Flag only significant bugs; ignore nitpicks and likely false positives. Do not flag issues that you cannot validate without looking at context outside of the git diff.

   Agent 4: Opus bug agent (parallel subagent with agent 3)
   Look for problems that exist in the introduced code. This could be security issues, incorrect logic, etc. Only look for issues that fall within the changed code.

   **CRITICAL: We only want HIGH SIGNAL issues.** Flag issues where:
   - The code will fail to compile or parse (syntax errors, type errors, missing imports, unresolved references)
   - The code will definitely produce wrong results regardless of inputs (clear logic errors)
   - Clear, unambiguous CLAUDE.md violations where you can quote the exact rule being broken

   Do NOT flag:
   - Code style or quality concerns
   - Potential issues that depend on specific inputs or state
   - Subjective suggestions or improvements

   If you are not certain an issue is real, do not flag it. False positives erode trust and waste reviewer time.

   In addition to the above, each subagent should be told the PR title and description. This will help provide context regarding the author's intent.

4. For each issue found in the previous step by agents 3 and 4, launch parallel subagents to validate the issue. These subagents should get the PR title and description along with a description of the issue. The agent's job is to review the issue to validate that the stated issue is truly an issue with high confidence. For example, if an issue such as "variable is not defined" was flagged, the subagent's job would be to validate that is actually true in the code. Another example would be CLAUDE.md issues. The agent should validate that the CLAUDE.md rule that was violated is scoped for this file and is actually violated. Use Opus subagents for bugs and logic issues, and sonnet agents for CLAUDE.md violations.

5. Filter out any issues that were not validated in step 4. This step will give us our list of high signal issues for our review.

6. Create a list of all inline comments that you plan on leaving. This is only for you to make sure you are comfortable with the comments. Do not post this list anywhere. If there are no new issues, skip to step 8.

7. Post inline comments for each issue using `mcp__github_inline_comment__create_inline_comment` with `confirmed: true`. For each comment:
   - Provide a brief description of the issue
   - For small, self-contained fixes, include a committable suggestion block
   - For larger fixes (6+ lines, structural changes, or changes spanning multiple locations), describe the issue and suggested fix without a suggestion block
   - Never post a committable suggestion UNLESS committing the suggestion fixes the issue entirely. If follow up steps are required, do not leave a committable suggestion.

   **IMPORTANT: Only post ONE comment per unique issue. Do not post duplicate comments.**

8. Always write the summary comment, also when nothing was found. There is only ever one summary comment per PR:
   - If a previous summary comment was found before step 1, update it in place: `gh api -X PATCH repos/<owner>/<repo>/issues/comments/<id> -f body="<summary>"`. This also applies when a re-review fell back to a full review.
   - Otherwise, create it with `gh pr comment <PR> --body "<summary>"`.

   Use exactly this format:

   ```
   ## Code review

   <"Full review." or "Re-review of the changes since <short sha>.">

   - [ ] <new or still open issue: brief description, link to the code>
   - [x] <previously found issue that is now fixed: brief description>

   <only if there are no open issues:> No issues found. Checked for bugs and CLAUDE.md compliance.

   <!-- claude-review-sha: <full head commit sha recorded before step 1> -->
   ```

   Leave out the checklist when it would be empty. The marker line must always be there, the next review uses it to find what changed.

   End your final message with the same summary, without the marker.

Use this list when evaluating issues in Steps 3 and 4 (these are false positives, do NOT flag):

- Pre-existing issues
- Something that appears to be a bug but is actually correct
- Pedantic nitpicks that a senior engineer would not flag
- Issues that a linter will catch (do not run the linter to verify)
- General code quality concerns (e.g., lack of test coverage, general security issues) unless explicitly required in CLAUDE.md
- Issues mentioned in CLAUDE.md but explicitly silenced in the code (e.g., via a lint ignore comment)

Notes:

- Use gh CLI to interact with GitHub (e.g., fetch pull requests, create comments). Do not use web fetch.
- Create a todo list before starting.
- You must cite and link each issue in inline comments (e.g., if referring to a CLAUDE.md, include a link to it).
- When linking to code in inline comments, follow the following format precisely, otherwise the Markdown preview won't render correctly: https://github.com/anthropics/claude-code/blob/c21d3c10bc8e898b7ac1a2d745bdc9bc4e423afe/package.json#L10-L15
  - Requires full git sha
  - You must provide the full sha. Commands like `https://github.com/owner/repo/blob/$(git rev-parse HEAD)/foo/bar` will not work, since your comment will be directly rendered in Markdown.
  - Repo name must match the repo you're code reviewing
  - # sign after the file name
  - Line range format is L[start]-L[end]
  - Provide at least 1 line of context before and after, centered on the line you are commenting about (eg. if you are commenting about lines 5-6, you should link to `L4-7`)
