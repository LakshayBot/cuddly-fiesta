namespace WorldEngine.Domain.Actions;

public interface IAgentAction
{
    string ActionType { get; }

    bool IsAvailable(ActionContext context);

    void Execute(ActionContext context);
}