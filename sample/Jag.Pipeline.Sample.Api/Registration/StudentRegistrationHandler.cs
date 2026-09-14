using Jag.Pipeline.Abstractions;
using Jag.Pipeline.Core;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Services;

namespace Jag.Pipeline.Sample.Api.Registration;

/// <summary>
/// Builds and runs the eight-step student registration pipeline:
/// 1. Sanity-check the request.
/// 2. Verify the identification number is not already registered.
/// 3. Verify the academic program exists.
/// 4. Insert the student with <c>Pending</c> status (compensable: remove it).
/// 5. Charge the registration fee (compensable: revert the charge).
/// 6. Activate the student in memory.
/// 7. Save the final state.
/// 8. Shape the response.
/// If any step fails, steps 4 and 5 (if reached) are compensated in reverse order before the
/// original exception is re-thrown, so a failed registration leaves no trace.
/// Every step shares the same <see cref="StudentRegistrationContext"/> and
/// <see cref="CancellationToken"/> contract, so the pipeline definition below is just a list of
/// method references.
/// </summary>
public sealed class StudentRegistrationHandler(
    IStudentService studentService,
    IProgramCatalogService programCatalogService,
    IPaymentService paymentService)
{
    public async Task<StudentGetModel> RegisterAsync(StudentAddModel request, CancellationToken ct)
    {
        var pipeline = new Pipeline<StudentRegistrationContext>()
            .AddStep(studentService.ValidateRequestAsync)
            .AddStep(studentService.EnsureIdentificationNumberIsUniqueAsync)
            .AddStep(programCatalogService.EnsureProgramExistsAsync)
            .AddCompensableStep(studentService.AddPendingAsync, studentService.RemovePendingAsync)
            .AddCompensableStep(paymentService.ChargeAsync, paymentService.RevertChargeAsync)
            .AddStep(studentService.CompleteRegistrationAsync)
            .AddStep(studentService.SaveChangesAsync)
            .AddStep(studentService.ShapeResponseAsync)
            .CreatePipeline();

        var context = new StudentRegistrationContext(request);
        var result = await pipeline.ExecuteAsync(context, ct);
        return result.Response!;
    }
}
