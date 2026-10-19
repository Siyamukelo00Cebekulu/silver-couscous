using GrandPrix.Core.Common;

namespace GrandPrix.Core.Abstractions;

public interface ICommand
{
    EntityId Issuer { get; }
}

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    CommandResult Handle(TCommand command);
}

public interface ICommandBus
{
    CommandResult Dispatch(ICommand command);
}
