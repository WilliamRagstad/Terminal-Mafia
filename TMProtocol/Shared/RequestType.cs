using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Shared
{
    public enum RequestType
    {
        CorruptData,
        Connect,
        LobbyUpdate,
        GameStart,
        GameEnd,
        AssignedRole,
        Story,
        Action,
        ActionSuccess,
        ActionError,
        ActionInfo
    }
}
