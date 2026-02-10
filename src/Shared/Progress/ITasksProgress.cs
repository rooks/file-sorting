namespace FileSorting.Shared.Progress;

public interface ITasksProgress
{
    void Start(string title, double max);
    void Stop();
    void Update(double value);
}
