﻿using NpgsqlTypes;
using System;

namespace Npgsql.Replication.PgOutput.Messages
{
    /// <summary>
    /// Logical Replication Protocol type message
    /// </summary>
    public sealed class TypeMessage : PgOutputReplicationMessage
    {
        /// <summary>
        /// ID of the data type.
        /// </summary>
        public uint TypeId { get; private set; }

        /// <summary>
        /// Namespace (empty string for pg_catalog).
        /// </summary>
        public string Namespace { get; private set; } = string.Empty;

        /// <summary>
        /// Name of the data type.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        internal TypeMessage Populate(
            NpgsqlLogSequenceNumber walStart, NpgsqlLogSequenceNumber walEnd, DateTime serverClock, uint typeId, string ns, string name)
        {
            base.Populate(walStart, walEnd, serverClock);

            TypeId = typeId;
            Namespace = ns;
            Name = name;

            return this;
        }

        /// <inheritdoc />
#if NET5_0_OR_GREATER
        public override TypeMessage Clone()
#else
        public override PgOutputReplicationMessage Clone()
#endif
        {
            var clone = new TypeMessage();
            clone.Populate(WalStart, WalEnd, ServerClock, TypeId, Namespace, Name);
            return clone;
        }
    }
}
