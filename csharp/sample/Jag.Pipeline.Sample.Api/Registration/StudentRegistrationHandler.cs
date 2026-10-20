using Jag.Pipeline.Abstractions;
using Jag.Pipeline.Core;
using Jag.Pipeline.Sample.Api.Models;
using Jag.Pipeline.Sample.Api.Pipeline.Services;

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
/// Every step shares the same <see cref="StudentRegistrationPipelineModel"/> and
/// <see cref="CancellationToken"/> contract, so the pipeline definition below is just a list of
/// method references.
/// </summary>
public sealed class StudentRegistrationHandler
{
    private IPipeline<StudentRegistrationPipelineModel> studentRegistrationPipeline;

    private readonly IStudentService studentService;
    private readonly IProgramCatalogService programCatalogService;
    private readonly IPaymentService paymentService;

    public StudentRegistrationHandler(
        IStudentService studentService,
        IProgramCatalogService programCatalogService,
        IPaymentService paymentService)
    {
        this.studentService = studentService;
        this.programCatalogService = programCatalogService;
        this.paymentService = paymentService;
        this.studentRegistrationPipeline = this.buildPipeline();
    }

    public async Task<StudentGetModel> RegisterAsync(StudentAddModel request, CancellationToken ct)
    {
        var registrationModel = new StudentRegistrationPipelineModel(
            request.FirstName,
            request.LastName,
            request.IdentificationNumber,
            request.ProgramId);

        var result = await this.studentRegistrationPipeline.ExecuteAsync(registrationModel, ct);

        return result.Response!;
    }

    private IPipeline<StudentRegistrationPipelineModel> buildPipeline()
        => new Pipeline<StudentRegistrationPipelineModel>()
            .AddStep(this.studentService.ValidateRequestAsync)
            .AddStep(this.studentService.EnsureIdentificationNumberIsUniqueAsync)
            .AddStep(this.programCatalogService.EnsureProgramExistsAsync)
            .AddCompensableStep(this.studentService.AddPendingAsync, this.studentService.RemovePendingAsync)
            .AddCompensableStep(this.paymentService.ChargeAsync, this.paymentService.RevertChargeAsync)
            .AddStep(this.studentService.CompleteRegistrationAsync)
            .AddStep(this.studentService.SaveChangesAsync)
            .AddStep(this.studentService.ShapeResponseAsync)
            .CreatePipeline();
}
