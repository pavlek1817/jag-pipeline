namespace JagPav.Pipeline.UnitTests.PipelineTests;

internal abstract class PipelineTestBase : TestBase
{
    protected static Task<StudentModel> addYear(StudentModel model, CancellationToken ct)
    {
        model.Age++;
        return Task.FromResult(model);
    }

    protected static Task<StudentModel> subtractYear(StudentModel model, CancellationToken ct)
    {
        model.Age--;
        return Task.FromResult(model);
    }

    protected static Task<TModel> addYearToAnyAged<TModel>(TModel model, CancellationToken ct)
        where TModel : IAged
    {
        model.Age++;
        return Task.FromResult(model);
    }

    protected static Task<StudentModel> calculateNameLength(StudentModel model, CancellationToken ct)
    {
        model.NameLength = model.Name.Length;
        return Task.FromResult(model);
    }

    protected interface IAged
    {
        int Age { get; set; }
    }

    protected sealed class StudentModel : IAged
    {
        public int Age { get; set; }

        internal string Name { get; init; } = string.Empty;

        internal int NameLength { get; set; }
    }
}
