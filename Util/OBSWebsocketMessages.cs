namespace LAHEE.Util;

// these are defined by obs-websocket, therefore disable checks

// ReSharper disable All
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

public class OBSMessage<T> {
    public int op;
    public T d;
}

public class OBSHello {
    public String obsStudioVersion;
    public String obsWebSocketVersion;
    public int rpcVersion;
    public Authentication authentication;

    public class Authentication {
        public String challenge;
        public String salt;
    }
}

public class OBSIdentify {
    public int rpcVersion;
    public String authentication;
    public int eventSubscriptions;
}

public class OBSIdentified {
    public int negotiatedRpcVersion;
}

public class OBSRequest<T> {
    public String requestType;
    public String requestId;
    public T requestData;
}

public class OBSResponse {
    public String requestType;
    public String requestId;
    public Status requestStatus;

    public class Status {
        public bool result;
        public int code;
        public String comment;
    }
}