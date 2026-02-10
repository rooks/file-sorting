namespace FileSorting.Shared.Progress;

public class FakeTasksProgress : ITasksProgress
{
    public void Start(string title, double max) { }
    public void Stop() { }
    public void Update(double value) { }
}
