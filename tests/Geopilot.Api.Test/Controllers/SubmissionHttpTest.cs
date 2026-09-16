using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Drives the machine delivery over HTTP instead of by calling the controller, because everything between the
/// two is where this resource has actually broken: the form value providers read the body before the action ran
/// and every multipart call answered 500, while the controller tests, which call the method directly, were green.
/// These tests cover only what the pipeline adds; the decision logic stays in <see cref="SubmissionControllerTest"/>.
/// <para>
/// Where an installation keeps its uploads is a property of the host, not of the request, so every test builds
/// the installation it describes and names the backend rather than relying on the default. Both routes are
/// published either way; which one an installation takes is decided in the action.
/// </para>
/// </summary>
[TestClass]
public class SubmissionHttpTest
{
    private const string MandateKey = "HTTPFALCON";
    private const string Boundary = "geopilot-http-test-boundary";

    /// <summary>
    /// Creates the mandate once for the whole class. The tests run in parallel (see AssemblyInfo), so leaving
    /// this to each of them means several racing to insert the same key and all but one failing on the unique
    /// index. It stays in the database afterwards, which is what lets a second run find it instead of clashing.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        using var app = new MachineDeliveryTestApp();
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        TestMachineClients.EnsureRegistered(context);
        if (context.Mandates.Any(m => m.Key == MandateKey))
            return;

        // Public, so any signed-in caller may deliver to it without wiring up an organisation. Committed rather
        // than rolled back, because each host reads it through its own connection.
        context.Mandates.Add(new Mandate
        {
            Name = TestHelpers.Localized(nameof(SubmissionHttpTest)),
            Key = MandateKey,
            IsPublic = true,
            AllowDelivery = true,
            PipelineId = "ili_validation",
            FileTypes = [".xtf"],
            EvaluateComment = FieldEvaluationType.NotEvaluated,
            EvaluatePartial = FieldEvaluationType.NotEvaluated,
            EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
        });
        context.SaveChanges();
    }

    [TestMethod]
    public async Task AcceptsAMultipartDeliverySentAsARequest()
    {
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation);
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(
            HttpStatusCode.Accepted,
            response.StatusCode,
            "MVC reads the body through its value providers for any form content type, before the action runs. Only a real request goes through them, which is why calling the action directly cannot show this.");
    }

    [TestMethod]
    public async Task AcceptsADeliveryFromARegisteredMachineClient()
    {
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation, JwtTestTokenBuilder.CreateValidClientToken());
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(
            HttpStatusCode.Accepted,
            response.StatusCode,
            "A token issued for client credentials names no person, and the identity provider of this host describes none for it. The registration has to be all the installation needs.");
    }

    [TestMethod]
    public async Task RefusesAMachineClientThatWasDeactivated()
    {
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation, JwtTestTokenBuilder.CreateClientToken(JwtTestTokenBuilder.InactiveClientSub));
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, "Deactivating a client is how an administrator revokes it without touching the identity provider.");
    }

    [TestMethod]
    public async Task RefusesAMachineNobodyRegistered()
    {
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation, JwtTestTokenBuilder.CreateClientToken(Guid.NewGuid().ToString()));
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            "A valid token alone makes no client: which machines deliver is the administrator's decision, not the identity provider's.");
    }

    [TestMethod]
    public async Task RefusesAMultipartBodyThatEndsEarly()
    {
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation);

        // Shorter than the form needs, and honestly declared as such: the reader runs out while looking for the
        // closing boundary. That is a fault of the request, not of the installation.
        var full = BuildMultipart(MandateKey, "data.xtf", Payload(16));
        using var content = AsMultipart(full, full.Length - 10);

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RefusesAFileLargerThanTheInstallationAccepts()
    {
        // The installation accepts one megabyte per file.
        using var installation = new MachineDeliveryTestApp();
        using var client = CreateAuthenticatedClient(installation);
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(2 * 1024 * 1024));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.IsEmpty(
            Directory.GetFiles(installation.RootDirectory, "*", SearchOption.AllDirectories),
            "A file that was refused for its size must not stay behind.");
    }

    [TestMethod]
    public async Task RefusesTheReferencingRouteWithoutAContentType()
    {
        // On the installation this route belongs to, so nothing else can be what refuses the request.
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = CreateAuthenticatedClient(installation);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/submission") { Content = new ByteArrayContent([]) };
        request.Content.Headers.ContentType = null;

        var response = await client.SendAsync(request);

        Assert.AreNotEqual(
            HttpStatusCode.InternalServerError,
            response.StatusCode,
            "Two actions with one Consumes each on the same route answer this with an ambiguous match, which is a 500. The two routes exist to keep that from happening, and only a real request selects an action.");
    }

    [TestMethod]
    public async Task AcceptsADeliveryThatReferencesAnUpload()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = CreateAuthenticatedClient(installation);

        // The client's own step before the delivery: the upload exists, and the attempt points at it. Its bytes
        // are not put in place here, because whether they arrived is what the preflight asks, not this call.
        using var initiate = JsonContent.Create(new { files = new[] { new { fileName = "data.xtf", size = 16 } } });
        var upload = await client.PostAsync("/api/v2/upload", initiate);
        Assert.AreEqual(HttpStatusCode.Created, upload.StatusCode);
        var uploadId = (await upload.Content.ReadFromJsonAsync<InitiateUploadResponse>())!.UploadId;

        using var body = JsonContent.Create(new { mandateKey = MandateKey, uploadId });
        var response = await client.PostAsync("/api/v1/submission", body);

        Assert.AreEqual(
            HttpStatusCode.Accepted,
            response.StatusCode,
            "Resolving the upload and checking its file types against the mandate only happens on this route; the inline form has no upload to resolve.");
        Assert.IsNotNull(
            response.Headers.Location,
            "The attempt is polled under the address it answers with, and that address is built by the routing of the running application. A direct call has to be handed a URL helper to produce it at all.");
    }

    [TestMethod]
    public async Task ReadsTheDeliveryDetailsFromAJsonBody()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = CreateAuthenticatedClient(installation);
        using var body = JsonContent.Create(new { mandateKey = MandateKey, uploadId = Guid.NewGuid() });

        var response = await client.PostAsync("/api/v1/submission", body);

        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode,
            "Reaching the upload lookup means the mandate was resolved from the body, so the JSON bound to SubmissionRequest. Nothing else shows that: the controller tests hand the action a request object they built themselves.");
    }

    [TestMethod]
    public async Task RefusesTheInlineFormWhereTheUploadsLiveOutsideTheApi()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = CreateAuthenticatedClient(installation);
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));

        var response = await client.PostAsync("/api/v1/submission/files", content);

        Assert.AreEqual(
            HttpStatusCode.BadRequest,
            response.StatusCode,
            "Both routes exist on every installation; which one it takes is decided in the action, not by routing.");
    }

    [TestMethod]
    public async Task DoesNotRouteTheResourceWhenMachineDeliveryIsDisabled()
    {
        using var installation = new MachineDeliveryTestApp(machineDeliveryEnabled: false);
        using var client = CreateAuthenticatedClient(installation);
        using var content = MultipartBody(MandateKey, "data.xtf", Payload(16));
        using var probe = new ByteArrayContent([]);

        var response = await client.PostAsync("/api/v1/submission/files", content);
        var neverExisted = await client.PostAsync("/api/v1/aroutethatneverexisted", probe);

        // Compared against a route that was never defined rather than against a fixed code, because what an
        // unmatched path answers is a property of this application, not of this resource: the authorization
        // fallback policy applies where no endpoint matched, so a signed-in non-admin gets 403 and not 404.
        Assert.AreEqual(
            neverExisted.StatusCode,
            response.StatusCode,
            "With the capability off the route has to be as absent as one that was never defined. Absent from the published API is not the same as absent from routing; the convention has to remove both.");
        Assert.IsEmpty(
            Directory.GetFiles(installation.RootDirectory, "*", SearchOption.AllDirectories),
            "Nothing may be read or stored for a resource this installation does not offer.");
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Context> app, string? token = null)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? JwtTestTokenBuilder.CreateValidUserToken());
        return client;
    }

    private static byte[] Payload(int size) => Encoding.UTF8.GetBytes(new string('x', size));

    private static ByteArrayContent MultipartBody(string mandateKey, string fileName, byte[] fileContent)
    {
        var body = BuildMultipart(mandateKey, fileName, fileContent);
        return AsMultipart(body, body.Length);
    }

    private static ByteArrayContent AsMultipart(byte[] body, int length)
    {
        var content = new ByteArrayContent(body, 0, length);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={Boundary}");
        return content;
    }

    /// <summary>
    /// Builds the form by hand, because the order of the parts is part of the contract: the fields come before
    /// the files so a wrong mandate is refused before any file content is read.
    /// </summary>
    private static byte[] BuildMultipart(string mandateKey, string fileName, byte[] fileContent)
    {
        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"--{Boundary}\r\n")
            .Append("Content-Disposition: form-data; name=\"mandateKey\"\r\n\r\n")
            .Append(CultureInfo.InvariantCulture, $"{mandateKey}\r\n")
            .Append(CultureInfo.InvariantCulture, $"--{Boundary}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n")
            .Append("Content-Type: application/octet-stream\r\n\r\n")
            .ToString();

        using var buffer = new MemoryStream();
        var headBytes = Encoding.UTF8.GetBytes(head);
        buffer.Write(headBytes, 0, headBytes.Length);
        buffer.Write(fileContent, 0, fileContent.Length);
        var tailBytes = Encoding.UTF8.GetBytes($"\r\n--{Boundary}--\r\n");
        buffer.Write(tailBytes, 0, tailBytes.Length);

        return buffer.ToArray();
    }
}
