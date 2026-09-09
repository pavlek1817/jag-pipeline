using Jag.Pipeline.Abstractions;
using Jag.Pipeline.Core;
using Jag.Pipeline.Sample.Api.Exceptions;
using Jag.Pipeline.Sample.Api.Mapping;
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
/// </summary>
public sealed class StudentRegistrationHandler(
    IStudentService studentService,
    IProgramCatalogService programCatalogService,
    IPaymentService paymentService,
    TimeProvider timeProvider)
{
    private const decimal RegistrationFee = 100m;

    public async Task<StudentGetModel> RegisterAsync(StudentAddModel request, CancellationToken ct)
    {
        var pipeline = new Pipeline<StudentRegistrationContext>()
            .AddStep((ctx, token) => validateRequestAsync(ctx))
            .AddStep((ctx, token) => studentService.EnsureIdentificationNumberIsUniqueAsync(ctx.Request.IdentificationNumber, token))
            .AddStep((ctx, token) => programCatalogService.EnsureProgramExistsAsync(ctx.Request.ProgramId, token))
            .AddCompensableStep(
                async (ctx, token) => ctx.Student = await studentService.AddPendingAsync(ctx.Request, token),
                async (ctx, token) => await studentService.RemovePendingAsync(ctx.Student!, token))
            .AddCompensableStep(
                async (ctx, token) => ctx.ChargeId = await paymentService.ChargeAsync(ctx.Student!.Id, RegistrationFee, token),
                async (ctx, token) => await paymentService.RevertChargeAsync(ctx.ChargeId!.Value, token))
            .AddStep((ctx, token) =>
            {
                studentService.CompleteRegistration(ctx.Student!, timeProvider.GetUtcNow().UtcDateTime.AddMonths(1));
                return Task.CompletedTask;
            })
            .AddStep((ctx, token) => studentService.SaveChangesAsync(token))
            .AddStep((ctx, token) =>
            {
                ctx.Response = StudentMapper.ToGetModel(ctx.Student!);
                return Task.CompletedTask;
            })
            .CreatePipeline();

        var context = new StudentRegistrationContext(request);
        var result = await pipeline.ExecuteAsync(context, ct);
        return result.Response!;
    }

    private static Task validateRequestAsync(StudentRegistrationContext ctx)
    {
        var errors = new List<string>();
        var request = ctx.Request;

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors.Add("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors.Add("Last name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdentificationNumber))
        {
            errors.Add("Identification number is required.");
        }

        if (request.ProgramId == Guid.Empty)
        {
            errors.Add("Program id is required.");
        }

        if (errors.Count > 0)
        {
            throw new StudentValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
