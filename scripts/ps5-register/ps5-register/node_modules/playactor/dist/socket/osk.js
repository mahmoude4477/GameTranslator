"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OskFlags = exports.OskInputType = exports.OskActionType = exports.OskCommand = void 0;
var OskCommand;
(function (OskCommand) {
    OskCommand[OskCommand["Close"] = 1] = "Close";
    OskCommand[OskCommand["Submit"] = 0] = "Submit";
})(OskCommand = exports.OskCommand || (exports.OskCommand = {}));
var OskActionType;
(function (OskActionType) {
    OskActionType[OskActionType["Default"] = 0] = "Default";
    OskActionType[OskActionType["Send"] = 1] = "Send";
    OskActionType[OskActionType["Search"] = 2] = "Search";
    OskActionType[OskActionType["Go"] = 3] = "Go";
})(OskActionType = exports.OskActionType || (exports.OskActionType = {}));
var OskInputType;
(function (OskInputType) {
    OskInputType[OskInputType["Default"] = 0] = "Default";
    OskInputType[OskInputType["BasicLatin"] = 1] = "BasicLatin";
    OskInputType[OskInputType["SimpleNumber"] = 2] = "SimpleNumber";
    OskInputType[OskInputType["ExtendedNumber"] = 3] = "ExtendedNumber";
    OskInputType[OskInputType["Url"] = 4] = "Url";
    OskInputType[OskInputType["Mail"] = 5] = "Mail";
})(OskInputType = exports.OskInputType || (exports.OskInputType = {}));
var OskFlags;
(function (OskFlags) {
    OskFlags[OskFlags["None"] = 0] = "None";
    OskFlags[OskFlags["MultiLine"] = 4] = "MultiLine";
    OskFlags[OskFlags["Password"] = 8] = "Password";
    OskFlags[OskFlags["AutoCapitalize"] = 1024] = "AutoCapitalize";
})(OskFlags = exports.OskFlags || (exports.OskFlags = {}));
