/* open62541 lab server for the Proxus software-qualification lab.
 *
 * Endpoints: None, and Basic256Sha256 / Aes128Sha256RsaOaep / Aes256Sha256RsaPss with Sign and SignAndEncrypt.
 * Identities: Anonymous and UserName operator/proxus-secret. Client application certificates are accepted
 * without a trust list because this is an isolated lab peer, not a deployment template.
 * Deterministic nodes live under Objects/Lab with string NodeIds "Lab.<Name>" in namespace 1. Every write to a
 * writable node increments Lab.WriteCount, which the fault harness reads to prove that an ambiguous write was
 * applied at most once. Writing any value to Lab.CloseSession makes the server close the writing Session right
 * after the Write response, while the SecureChannel stays open (UA_Server_closeSession); the session stage of the
 * lab uses it to make a server-side Session close deterministic.
 */
#include <open62541/plugin/accesscontrol_default.h>
#include <open62541/plugin/pki_default.h>
#include <open62541/plugin/historydata/history_data_backend_memory.h>
#include <open62541/plugin/historydata/history_data_gathering_default.h>
#include <open62541/plugin/historydata/history_database_default.h>
#include <open62541/plugin/historydatabase.h>
#include <open62541/plugin/log_stdout.h>
#include <open62541/server.h>
#include <open62541/server_config_default.h>

#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static volatile UA_Boolean running = true;
static UA_UInt32 writeCount = 0;
static UA_NodeId labFolder;
/* Session to close after the current server iteration; closing it inside its own Write callback is unsafe. */
static UA_NodeId pendingClose;
static UA_Boolean closePending = false;

static void stopHandler(int sig) { (void)sig; running = false; }

static UA_ByteString loadFile(const char *path) {
    UA_ByteString content = UA_BYTESTRING_NULL;
    FILE *fp = fopen(path, "rb");
    if(!fp) { fprintf(stderr, "cannot open %s\n", path); exit(2); }
    fseek(fp, 0, SEEK_END);
    content.length = (size_t)ftell(fp);
    content.data = (UA_Byte *)UA_malloc(content.length);
    fseek(fp, 0, SEEK_SET);
    if(fread(content.data, 1, content.length, fp) != content.length) { fclose(fp); exit(2); }
    fclose(fp);
    return content;
}

static void afterWrite(UA_Server *server, const UA_NodeId *sessionId, void *sessionContext,
                       const UA_NodeId *nodeId, void *nodeContext, const UA_NumericRange *range,
                       const UA_DataValue *data) {
    (void)sessionId; (void)sessionContext; (void)nodeId; (void)nodeContext; (void)range; (void)data;
    writeCount++;
    UA_Variant value;
    UA_Variant_setScalar(&value, &writeCount, &UA_TYPES[UA_TYPES_UINT32]);
    UA_Server_writeValue(server, UA_NODEID_STRING(1, "Lab.WriteCount"), value);
}

static void requestSessionClose(UA_Server *server, const UA_NodeId *sessionId, void *sessionContext,
                                const UA_NodeId *nodeId, void *nodeContext, const UA_NumericRange *range,
                                const UA_DataValue *data) {
    (void)server; (void)sessionContext; (void)nodeId; (void)nodeContext; (void)range; (void)data;
    if(closePending) UA_NodeId_clear(&pendingClose);
    UA_NodeId_copy(sessionId, &pendingClose);
    closePending = true;
}

static void addVariable(UA_Server *server, const char *name, const void *data, size_t arrayLength,
                        const UA_DataType *type, UA_Boolean writable) {
    char id[64];
    snprintf(id, sizeof(id), "Lab.%s", name);
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_UInt32 dimensions[1] = {(UA_UInt32)arrayLength};
    if(arrayLength == 0) {
        UA_Variant_setScalarCopy(&attr.value, data, type);
        attr.valueRank = UA_VALUERANK_SCALAR;
    } else {
        UA_Variant_setArrayCopy(&attr.value, data, arrayLength, type);
        attr.valueRank = UA_VALUERANK_ONE_DIMENSION;
        attr.arrayDimensionsSize = 1;
        attr.arrayDimensions = dimensions;
    }
    attr.dataType = type->typeId;
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)name);
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | (writable ? UA_ACCESSLEVELMASK_WRITE : 0);
    attr.userAccessLevel = attr.accessLevel;
    UA_NodeId nodeId = UA_NODEID_STRING(1, id);
    UA_StatusCode rc = UA_Server_addVariableNode(server, nodeId, labFolder,
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES), UA_QUALIFIEDNAME(1, (char *)name),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE), attr, NULL, NULL);
    UA_Variant_clear(&attr.value);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "add %s failed %s\n", id, UA_StatusCode_name(rc)); exit(3); }
    if(writable) {
        UA_ValueCallback callback = {NULL, strcmp(name, "CloseSession") == 0 ? requestSessionClose : afterWrite};
        UA_Server_setVariableNode_valueCallback(server, nodeId, callback);
    }
}

static void addLabNodes(UA_Server *server) {
    UA_ObjectAttributes folder = UA_ObjectAttributes_default;
    folder.displayName = UA_LOCALIZEDTEXT("en-US", "Lab");
    labFolder = UA_NODEID_STRING(1, "Lab");
    UA_Server_addObjectNode(server, labFolder, UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES), UA_QUALIFIEDNAME(1, "Lab"),
        UA_NODEID_NUMERIC(0, UA_NS0ID_FOLDERTYPE), folder, NULL, NULL);

    UA_Boolean b = true; UA_SByte sb = -12; UA_Byte by = 200; UA_Int16 i16 = -12345; UA_UInt16 u16 = 54321;
    UA_Int32 i32 = -123456789; UA_UInt32 u32 = 3000000000u; UA_Int64 i64 = -9007199254740993LL;
    UA_UInt64 u64 = 18000000000000000000ULL; UA_Float f = 123.25f; UA_Double d = 42.5;
    UA_String s = UA_STRING("proxus-lab \xC3\x84\xC3\x96\xC3\x9C \xE2\x82\xAC");
    UA_DateTime dt = UA_DateTime_fromUnixTime(1767225600); /* 2026-01-01T00:00:00Z */
    UA_Int32 i32a[10] = {1, -2, 3, -4, 5, -6, 7, -8, 2147483647, (UA_Int32)-2147483647 - 1};
    UA_Double da[5] = {0.5, -1.25, 1e300, -1e-300, 3.141592653589793};
    UA_Boolean ba[6] = {true, false, true, true, false, true};
    UA_Double writeTarget = 0.0; UA_Int32 writeTargetInt = 0; UA_UInt32 zero = 0; UA_Boolean closeFlag = false;

    addVariable(server, "Boolean", &b, 0, &UA_TYPES[UA_TYPES_BOOLEAN], false);
    addVariable(server, "SByte", &sb, 0, &UA_TYPES[UA_TYPES_SBYTE], false);
    addVariable(server, "Byte", &by, 0, &UA_TYPES[UA_TYPES_BYTE], false);
    addVariable(server, "Int16", &i16, 0, &UA_TYPES[UA_TYPES_INT16], false);
    addVariable(server, "UInt16", &u16, 0, &UA_TYPES[UA_TYPES_UINT16], false);
    addVariable(server, "Int32", &i32, 0, &UA_TYPES[UA_TYPES_INT32], false);
    addVariable(server, "UInt32", &u32, 0, &UA_TYPES[UA_TYPES_UINT32], false);
    addVariable(server, "Int64", &i64, 0, &UA_TYPES[UA_TYPES_INT64], false);
    addVariable(server, "UInt64", &u64, 0, &UA_TYPES[UA_TYPES_UINT64], false);
    addVariable(server, "Float", &f, 0, &UA_TYPES[UA_TYPES_FLOAT], false);
    addVariable(server, "Double", &d, 0, &UA_TYPES[UA_TYPES_DOUBLE], false);
    addVariable(server, "String", &s, 0, &UA_TYPES[UA_TYPES_STRING], false);
    addVariable(server, "DateTime", &dt, 0, &UA_TYPES[UA_TYPES_DATETIME], false);
    addVariable(server, "Int32Array", i32a, 10, &UA_TYPES[UA_TYPES_INT32], false);
    addVariable(server, "DoubleArray", da, 5, &UA_TYPES[UA_TYPES_DOUBLE], false);
    addVariable(server, "BooleanArray", ba, 6, &UA_TYPES[UA_TYPES_BOOLEAN], false);
    addVariable(server, "WriteCount", &zero, 0, &UA_TYPES[UA_TYPES_UINT32], false);
    addVariable(server, "WriteDouble", &writeTarget, 0, &UA_TYPES[UA_TYPES_DOUBLE], true);
    addVariable(server, "WriteInt32", &writeTargetInt, 0, &UA_TYPES[UA_TYPES_INT32], true);
    addVariable(server, "CloseSession", &closeFlag, 0, &UA_TYPES[UA_TYPES_BOOLEAN], true);
}

/* Lab.Add(Int32 a, Int32 b) -> Int32 sum, a method on the Lab folder. open62541 validates the
 * argument count and types before the callback runs. */
static UA_StatusCode addMethod(UA_Server *server, const UA_NodeId *sessionId, void *sessionHandle,
                               const UA_NodeId *methodId, void *methodContext, const UA_NodeId *objectId,
                               void *objectContext, size_t inputSize, const UA_Variant *input,
                               size_t outputSize, UA_Variant *output) {
    UA_Int32 sum = *(UA_Int32 *)input[0].data + *(UA_Int32 *)input[1].data;
    return UA_Variant_setScalarCopy(output, &sum, &UA_TYPES[UA_TYPES_INT32]);
}

static void addMethodNode(UA_Server *server) {
    UA_Argument inputs[2];
    for(int i = 0; i < 2; i++) {
        UA_Argument_init(&inputs[i]);
        inputs[i].dataType = UA_TYPES[UA_TYPES_INT32].typeId;
        inputs[i].valueRank = UA_VALUERANK_SCALAR;
    }
    inputs[0].name = UA_STRING("a");
    inputs[1].name = UA_STRING("b");
    UA_Argument output;
    UA_Argument_init(&output);
    output.name = UA_STRING("sum");
    output.dataType = UA_TYPES[UA_TYPES_INT32].typeId;
    output.valueRank = UA_VALUERANK_SCALAR;
    UA_MethodAttributes attr = UA_MethodAttributes_default;
    attr.displayName = UA_LOCALIZEDTEXT("en-US", "Add");
    attr.executable = true;
    attr.userExecutable = true;
    UA_StatusCode rc = UA_Server_addMethodNode(server, UA_NODEID_STRING(1, "Lab.Add"), labFolder,
        UA_NODEID_NUMERIC(0, UA_NS0ID_HASCOMPONENT), UA_QUALIFIEDNAME(1, "Add"), attr, &addMethod,
        2, inputs, 1, &output, NULL, NULL);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "add Lab.Add failed %s\n", UA_StatusCode_name(rc)); exit(3); }
}

/* Events: LabEventType (subtype of BaseEventType) and Lab.RaiseEvent(UInt16 severity, String message),
 * which triggers one LabEventType event with SourceName "LabSource" on the Server object. */
static UA_NodeId labEventType;

static UA_StatusCode raiseEventMethod(UA_Server *server, const UA_NodeId *sessionId, void *sessionHandle,
                                      const UA_NodeId *methodId, void *methodContext, const UA_NodeId *objectId,
                                      void *objectContext, size_t inputSize, const UA_Variant *input,
                                      size_t outputSize, UA_Variant *output) {
    UA_NodeId eventId;
    UA_StatusCode rc = UA_Server_createEvent(server, labEventType, &eventId);
    if(rc != UA_STATUSCODE_GOOD) return rc;
    UA_UInt16 severity = *(UA_UInt16 *)input[0].data;
    UA_String *text = (UA_String *)input[1].data;
    UA_LocalizedText message;
    message.locale = UA_STRING("en-US");
    message.text = *text;
    UA_String source = UA_STRING("LabSource");
    UA_DateTime now = UA_DateTime_now();
    const char *names[4] = {"Severity", "Message", "SourceName", "Time"};
    const void *values[4] = {&severity, &message, &source, &now};
    const UA_DataType *types[4] = {&UA_TYPES[UA_TYPES_UINT16], &UA_TYPES[UA_TYPES_LOCALIZEDTEXT],
                                   &UA_TYPES[UA_TYPES_STRING], &UA_TYPES[UA_TYPES_DATETIME]};
    for(int i = 0; i < 4; i++) {
        rc = UA_Server_writeObjectProperty_scalar(server, eventId, UA_QUALIFIEDNAME(0, (char *)names[i]),
                                                  values[i], types[i]);
        if(rc != UA_STATUSCODE_GOOD) {
            fprintf(stderr, "LAB event property %s: %s\n", names[i], UA_StatusCode_name(rc));
            return rc;
        }
    }
    rc = UA_Server_triggerEvent(server, eventId, UA_NODEID_NUMERIC(0, UA_NS0ID_SERVER), NULL, UA_TRUE);
    if(rc != UA_STATUSCODE_GOOD) fprintf(stderr, "LAB triggerEvent: %s\n", UA_StatusCode_name(rc));
    return rc;
}

static void addEventNodes(UA_Server *server) {
    UA_ObjectTypeAttributes typeAttr = UA_ObjectTypeAttributes_default;
    typeAttr.displayName = UA_LOCALIZEDTEXT("en-US", "LabEventType");
    labEventType = UA_NODEID_STRING(1, "LabEventType");
    UA_StatusCode rc = UA_Server_addObjectTypeNode(server, labEventType, UA_NODEID_NUMERIC(0, UA_NS0ID_BASEEVENTTYPE),
        UA_NODEID_NUMERIC(0, UA_NS0ID_HASSUBTYPE), UA_QUALIFIEDNAME(1, "LabEventType"), typeAttr, NULL, NULL);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "add LabEventType failed %s\n", UA_StatusCode_name(rc)); exit(3); }

    UA_Argument inputs[2];
    UA_Argument_init(&inputs[0]);
    inputs[0].name = UA_STRING("severity");
    inputs[0].dataType = UA_TYPES[UA_TYPES_UINT16].typeId;
    inputs[0].valueRank = UA_VALUERANK_SCALAR;
    UA_Argument_init(&inputs[1]);
    inputs[1].name = UA_STRING("message");
    inputs[1].dataType = UA_TYPES[UA_TYPES_STRING].typeId;
    inputs[1].valueRank = UA_VALUERANK_SCALAR;
    UA_MethodAttributes attr = UA_MethodAttributes_default;
    attr.displayName = UA_LOCALIZEDTEXT("en-US", "RaiseEvent");
    attr.executable = true;
    attr.userExecutable = true;
    rc = UA_Server_addMethodNode(server, UA_NODEID_STRING(1, "Lab.RaiseEvent"), labFolder,
        UA_NODEID_NUMERIC(0, UA_NS0ID_HASCOMPONENT), UA_QUALIFIEDNAME(1, "RaiseEvent"), attr, &raiseEventMethod,
        2, inputs, 0, NULL, NULL, NULL);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "add Lab.RaiseEvent failed %s\n", UA_StatusCode_name(rc)); exit(3); }
}

/* Lab.History: a Double variable historized in memory (open62541 history plugin) with twelve
 * deterministic values at 2026-01-01T00:00:00Z + i s, value i * 1.5. At most five values per response,
 * so a raw read of all twelve needs continuation points. */
#define LAB_HISTORY_VALUES 12
static void addHistoryNode(UA_Server *server, UA_HistoryDataGathering *gathering) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_Double initial = 0.0;
    UA_Variant_setScalar(&attr.value, &initial, &UA_TYPES[UA_TYPES_DOUBLE]);
    attr.dataType = UA_TYPES[UA_TYPES_DOUBLE].typeId;
    attr.displayName = UA_LOCALIZEDTEXT("en-US", "History");
    attr.historizing = true;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_HISTORYREAD;
    attr.userAccessLevel = attr.accessLevel;
    UA_NodeId nodeId = UA_NODEID_STRING(1, "Lab.History");
    UA_StatusCode rc = UA_Server_addVariableNode(server, nodeId, labFolder,
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES), UA_QUALIFIEDNAME(1, "History"),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE), attr, NULL, NULL);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "add Lab.History failed %s\n", UA_StatusCode_name(rc)); exit(3); }

    UA_HistorizingNodeIdSettings setting;
    memset(&setting, 0, sizeof(setting));
    setting.historizingBackend = UA_HistoryDataBackend_Memory(1, 100);
    setting.maxHistoryDataResponseSize = 5;
    setting.historizingUpdateStrategy = UA_HISTORIZINGUPDATESTRATEGY_VALUESET;
    rc = gathering->registerNodeId(server, gathering->context, &nodeId, setting);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "historize failed %s\n", UA_StatusCode_name(rc)); exit(3); }

    UA_DateTime start = UA_DateTime_fromUnixTime(1767225600); /* 2026-01-01T00:00:00Z */
    for(int i = 0; i < LAB_HISTORY_VALUES; i++) {
        UA_Double value = i * 1.5;
        UA_DataValue dv;
        UA_DataValue_init(&dv);
        UA_Variant_setScalar(&dv.value, &value, &UA_TYPES[UA_TYPES_DOUBLE]);
        dv.hasValue = true;
        dv.sourceTimestamp = start + (UA_DateTime)i * UA_DATETIME_SEC;
        dv.hasSourceTimestamp = true;
        dv.serverTimestamp = dv.sourceTimestamp;
        dv.hasServerTimestamp = true;
        rc = UA_Server_writeDataValue(server, nodeId, dv);
        if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "history value failed %s\n", UA_StatusCode_name(rc)); exit(3); }
    }
}

int main(int argc, char **argv) {
    const char *certPath = argc > 1 ? argv[1] : "/lab/pki/server.der";
    const char *keyPath = argc > 2 ? argv[2] : "/lab/pki/server-key.der";
    const char *hostname = getenv("LAB_OPCUA_HOSTNAME");
    signal(SIGINT, stopHandler);
    signal(SIGTERM, stopHandler);

    UA_ByteString certificate = loadFile(certPath);
    UA_ByteString privateKey = loadFile(keyPath);
    UA_Server *server = UA_Server_new();
    UA_ServerConfig *config = UA_Server_getConfig(server);
    UA_StatusCode rc = UA_ServerConfig_setDefaultWithSecurityPolicies(config, 4840, &certificate, &privateKey,
                                                                      NULL, 0, NULL, 0, NULL, 0);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "config: %s\n", UA_StatusCode_name(rc)); return 1; }

    /* Lab only: accept every client application certificate. */
    config->secureChannelPKI.clear(&config->secureChannelPKI);
    UA_CertificateVerification_AcceptAll(&config->secureChannelPKI);
    config->sessionPKI.clear(&config->sessionPKI);
    UA_CertificateVerification_AcceptAll(&config->sessionPKI);

    UA_String_clear(&config->applicationDescription.applicationUri);
    config->applicationDescription.applicationUri = UA_STRING_ALLOC("urn:proxus:lab:open62541");
    if(hostname) {
        char url[256];
        snprintf(url, sizeof(url), "opc.tcp://%s:4840", hostname);
        UA_Array_delete(config->serverUrls, config->serverUrlsSize, &UA_TYPES[UA_TYPES_STRING]);
        config->serverUrls = UA_String_new();
        *config->serverUrls = UA_STRING_ALLOC(url);
        config->serverUrlsSize = 1;
    }

    UA_UsernamePasswordLogin logins[1] = {{UA_STRING_STATIC("operator"), UA_STRING_STATIC("proxus-secret")}};
    config->accessControl.clear(&config->accessControl);
    rc = UA_AccessControl_default(config, true,
        &config->securityPolicies[config->securityPoliciesSize - 1].policyUri, 1, logins);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "access control: %s\n", UA_StatusCode_name(rc)); return 1; }

    UA_HistoryDataGathering gathering = UA_HistoryDataGathering_Default(1);
    config->historyDatabase = UA_HistoryDatabase_default(gathering);
    config->accessHistoryDataCapability = true;
    config->maxReturnDataValues = 0;

    addLabNodes(server);
    addHistoryNode(server, &gathering);
    addMethodNode(server);
    addEventNodes(server);
    UA_ByteString_clear(&certificate);
    UA_ByteString_clear(&privateKey);
    printf("READY open62541 %s\n", UA_OPEN62541_VERSION);
    fflush(stdout);
    rc = UA_Server_run_startup(server);
    if(rc != UA_STATUSCODE_GOOD) { fprintf(stderr, "startup: %s\n", UA_StatusCode_name(rc)); return 1; }
    while(running) {
        UA_Server_run_iterate(server, true);
        if(closePending) {
            UA_StatusCode closed = UA_Server_closeSession(server, &pendingClose);
            printf("LAB closeSession %s\n", UA_StatusCode_name(closed));
            fflush(stdout);
            UA_NodeId_clear(&pendingClose);
            closePending = false;
        }
    }
    rc = UA_Server_run_shutdown(server);
    UA_Server_delete(server);
    return rc == UA_STATUSCODE_GOOD ? 0 : 1;
}
