using System;

namespace OpenIIoT.SDK.Package.Packaging
{
    public class PackagingUpdateEventArgs : EventArgs
    {
        #region Public Constructors

        public PackagingUpdateEventArgs(PackagingOperation operation, PackagingUpdateType type, string message) : base()
        {
            Operation = operation;
            Type = type;
            Message = message;
        }

        #endregion Public Constructors

        #region Public Properties

        public string Message { get; private set; }
        public PackagingOperation Operation { get; private set; }
        public PackagingUpdateType Type { get; private set; }

        #endregion Public Properties
    }
}