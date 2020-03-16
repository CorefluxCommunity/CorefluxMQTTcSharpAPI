﻿/*
MQTT Project - MQTT Client Library for .Net and GnatMQ MQTT Broker for .NET
Copyright (c) 2014, Paolo Patierno, All rights reserved.

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 3.0 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library.
*/

#if (!MF_FRAMEWORK_VERSION_V4_2 && !MF_FRAMEWORK_VERSION_V4_3)
using System;
#else
using Microsoft.SPOT;
#endif

namespace Coreflux.API.cSharp.Networking.MQTT.Messages
{
    /// <summary>
    /// Event Args class for CONNECT message received from client
    /// </summary>
    public class MQTTMsgConnectEventArgs : EventArgs
    {
        /// <summary>
        /// Message received from client
        /// </summary>
        public MQTTMsgConnect Message { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="msg">CONNECT message received from client</param>
        public MQTTMsgConnectEventArgs(MQTTMsgConnect connect)
        {
            this.Message = connect;
        }
    }
} 
