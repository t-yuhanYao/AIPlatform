﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Luna.Clients.Models.Controller
{
    public class GetAllDeployOperationsByEndpointIdAndVerifyUserResponse
    {
        public List<Operation> operations { get; set; }
        public class Operation
        {
            public string operationType { get; set; }
            public string endpointId { get; set; }
            public string status { get; set; }
            public string startTimeUtc { get; set; }
            public string completeTimeUtc { get; set; }
            public Object error { get; set; }
        }
        public GetAllDeployOperationsByEndpointIdAndVerifyUserResponse()
        {
            this.operations = new List<Operation>();
        }
    }
}
