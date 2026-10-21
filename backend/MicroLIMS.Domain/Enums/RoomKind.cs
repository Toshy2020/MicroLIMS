namespace MicroLIMS.Domain.Enums;

// Persisted as int; append only. An LAF (laminar air flow unit) is a
// monitored target in its own right, sitting inside a Room.
public enum RoomKind
{
    Room,
    LAF
}
