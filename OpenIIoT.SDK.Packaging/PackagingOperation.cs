using System;

namespace OpenIIoT.SDK.Package.Packaging
{
    public abstract class PackagingOperation
    {
        #region Public Constructors

        public PackagingOperation(PackagingOperationType type)
        {
            Type = type;
        }

        #endregion Public Constructors

        #region Private Properties

        private PackagingOperationType Type { get; set; }

        #endregion Private Properties

        #region Public Events

        /// <summary>
        ///     Raised when a new status message is generated.
        /// </summary>
        public event EventHandler<PackagingUpdateEventArgs> Updated;

        #endregion Public Events

        #region Protected Methods

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Info"/>.
        /// </summary>
        protected void Info(string message)
        {
            OnUpdated(PackagingUpdateType.Info, message);
        }

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Success"/>.
        /// </summary>
        protected void Success(string message)
        {
            OnUpdated(PackagingUpdateType.Success, message);
        }

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with a message of type <see cref="PackagingUpdateType.Verbose"/>.
        /// </summary>
        protected void Verbose(string message)
        {
            OnUpdated(PackagingUpdateType.Verbose, message);
        }

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        ///     Raises the <see cref="Updated"/> event with the specified message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void OnUpdated(PackagingUpdateType type, string message)
        {
            if (Updated != null)
            {
                Updated(null, new PackagingUpdateEventArgs(PackagingOperationType.ExtractManifest, type, message));
            }
        }

        #endregion Private Methods
    }
}