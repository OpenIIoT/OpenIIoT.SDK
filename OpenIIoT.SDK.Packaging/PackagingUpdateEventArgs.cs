﻿using System;

namespace OpenIIoT.SDK.Packaging
{
    public class PackagingUpdateEventArgs : EventArgs
    {
        #region Public Constructors

        public PackagingUpdateEventArgs(PackagingOperationType operation, PackagingUpdateType type, string message) : base()
        {
            Operation = operation;
            Type = type;
            Message = message;
        }

        #endregion Public Constructors

        #region Public Properties

        public string Message { get; private set; }
        public PackagingOperationType Operation { get; private set; }
        public PackagingUpdateType Type { get; private set; }

        #endregion Public Properties
    }
}