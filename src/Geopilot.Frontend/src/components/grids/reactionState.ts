import { ReactionState } from "../../api/generated";
import { TranslationKey } from "../../appInterfaces";

/** The name of each state of a reaction. */
export const reactionStateLabelKey: Record<ReactionState, TranslationKey> = {
  [ReactionState.Pending]: "reactionStatePending",
  [ReactionState.Running]: "reactionStateRunning",
  [ReactionState.Done]: "reactionStateDone",
  [ReactionState.Error]: "reactionStateError",
  [ReactionState.Deleted]: "reactionStateDeleted",
};

/** The color each state of a reaction is shown in. */
export const reactionStateColor: Record<ReactionState, string> = {
  [ReactionState.Pending]: "primary.main",
  [ReactionState.Running]: "primary.main",
  [ReactionState.Done]: "primary.main",
  [ReactionState.Error]: "error.main",
  [ReactionState.Deleted]: "primary.states.disabledBackground",
};
