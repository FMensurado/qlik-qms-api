﻿using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using QvQms.QMSAPI;

namespace QvQms
{
    class ServiceKeyBehaviorExtensionElement : BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(ServiceKeyEndpointBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new ServiceKeyEndpointBehavior();
        }
    }

    class ServiceKeyEndpointBehavior : IEndpointBehavior
    {
        public void Validate(ServiceEndpoint endpoint) { }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new ServiceKeyClientMessageInspector());
        }
    }

    class ServiceKeyClientMessageInspector : IClientMessageInspector
    {
        private const string SERVICE_KEY_HTTP_HEADER = "X-Service-Key";

        public static string ServiceKey { get; set; }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            object httpRequestMessageObject;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
            {
                HttpRequestMessageProperty httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                if (httpRequestMessage != null)
                {
                    httpRequestMessage.Headers[SERVICE_KEY_HTTP_HEADER] = (ServiceKey ?? string.Empty);
                }
                else
                {
                    httpRequestMessage = new HttpRequestMessageProperty();
                    httpRequestMessage.Headers.Add(SERVICE_KEY_HTTP_HEADER, (ServiceKey ?? string.Empty));
                    request.Properties[HttpRequestMessageProperty.Name] = httpRequestMessage;
                }
            }
            else
            {
                HttpRequestMessageProperty httpRequestMessage = new HttpRequestMessageProperty();
                httpRequestMessage.Headers.Add(SERVICE_KEY_HTTP_HEADER, (ServiceKey ?? string.Empty));
                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
            }
            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState) { }
    }

    class Program
    {
        private static IQMS apiClient;

        static void PrintUsage()
        {
            Console.WriteLine("QvQms usage:");
            Console.WriteLine("    -h shows usage");
            Console.WriteLine();
            Console.WriteLine("  tasks");
            Console.WriteLine("    -ta list all tasks");
            Console.WriteLine("    -tf <name> find task by name");
            Console.WriteLine("    -t <id> get task by id");
            Console.WriteLine();
            Console.WriteLine("  documents");
            Console.WriteLine("    -da list all user documents");
            Console.WriteLine("    -du list all user documents access entries");
            Console.WriteLine("    -d-add-access <doc> <username|groupname> adds user or group to doc authorization list");
        }

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h")
            {
                PrintUsage();
                return;
            }

            //try
            //{
                // create a QMS API client
                apiClient = new QMSClient();

                //If you want to connect to a server different from the one used when creating the service reference,
                //do as follows:                                
                // 
                //NTLM only (default installation)
                //IQMS apiClient = new QMSClient("BasicHttpBinding_IQMS", "http://remotehost:4799/QMS/Service");
                // 
                //Certificate security
                //IQMS apiClient = new QMSClient("WSHttpBinding_IQMS", "https://remotehost:4799/QMS/Service");


                // retrieve a time limited service key
                ServiceKeyClientMessageInspector.ServiceKey = apiClient.GetTimeLimitedServiceKey();
                
                // QVS services
                List<ServiceInfo> qvs = apiClient.GetServices(ServiceTypes.QlikViewServer);
                // QDS services
                List<ServiceInfo> qms = apiClient.GetServices(ServiceTypes.QlikViewManagementService);

                if (qms.Count == 0)
                    throw new ApplicationException("Could not retrieve QDS ID.");

                if (qvs.Count == 0)
                    throw new ApplicationException("Could not retrieve QVS ID.");

                switch (args[0])
                {
                    case "-ta":
                        List<TaskInfo> tasks = apiClient.GetTasks(qms[0].ID);

                        WriteTaskHeader();
                        foreach (TaskInfo ti in tasks) WriteTaskInfo(ti);
                        break;
                    case "-tf":
                        if (args.Length >= 2)
                        {
                            TaskInfo ti = apiClient.FindTask(qms[0].ID, TaskType.Undefined, args[1]);
                            WriteTaskHeader();
                            WriteTaskInfo(ti);
                        }
                        else
                            throw new ApplicationException("Missing <name> parameter.");
                        break;
                    case "-t":
                        if (args.Length >= 2)
                        {
                            TaskInfo ti = apiClient.GetTask(new Guid(args[1]));
                            WriteTaskHeader();
                            WriteTaskInfo(ti);
                        }
                        else
                            throw new ApplicationException("Missing <id> parameter.");
                        break;
                    case "-da":
                        OutputUserDocuments(qvs[0].ID);
                        break;
                    case "-du":
                        OutputUserDocumentsAccessEntries(qvs[0].ID);
                        break;
                    case "-d-add-access":
                        AddAuthorization(qvs[0].ID, args[1], args[2]);
                        break;
                    case "-d-remove-access":
                        RemoveAuthorization(qvs[0].ID, args[1], args[2]);
                        break;
                }


                //if (qvsServices.Count > 0)
                //{
                //    // retrieve folder settings for the first QVS in the list
                //    QVSSettings qvsSettings = apiClient.GetQVSSettings(qvsServices[0].ID, QVSSettingsScope.Folders);
                //    // add a new mount
                //    qvsSettings.Folders.UserDocumentMounts.Add(new QVSMount() { Browsable = false, Path = @"\\unc\some", Name = "MyMount" });
                //    // save settings
                //    apiClient.SaveQVSSettings(qvsSettings);
                //    Console.WriteLine("Settings saved. New mount added.");
                //}
            //}
            //catch (System.Exception ex)
            //{
            //    Console.WriteLine("An exception occurred: " + ex.Message);
            //}
            // wait for user to press any key
            //Console.ReadLine();
        }

        private static void RemoveAuthorization(Guid qvsId, string doc, string name)
        {
            DocumentNode docNode = FindDoc(qvsId, doc);
            DocumentMetaData dm = apiClient.GetDocumentMetaData(docNode, DocumentMetaDataScope.Authorization);
            DocumentAccessEntry dae = dm.Authorization.Access.Find(e => e.UserName == name);
            dm.Authorization.Access.Remove(dae);
            apiClient.SaveDocumentMetaData(dm);
        }

        private static void AddAuthorization(Guid qvsId,  string doc, string name)
        {
            DocumentNode docNode = FindDoc(qvsId, doc);
            DocumentMetaData dm = apiClient.GetDocumentMetaData(docNode, DocumentMetaDataScope.Authorization);
            dm.Authorization.Access.Add(GetNewDocumentAcessEntry(name));
            apiClient.SaveDocumentMetaData(dm);
        }

        private static DocumentNode FindDoc(Guid qvsId, string doc)
        {
            List<DocumentNode> docs = apiClient.GetUserDocuments(qvsId);
            DocumentNode docNode = docs.Find(dn => dn.Name == doc);
            return docNode;
        }

        private static DocumentAccessEntry GetNewDocumentAcessEntry(string name)
        {
            DocumentAccessEntry dae = new DocumentAccessEntry();
            dae.AccessMode = DocumentAccessEntryMode.Always;
            dae.UserName = name;
            dae.IsAnonymous = false;
            dae.DayOfWeekConstraints = new List<DayOfWeek>();
            return dae;
        }

        private static void OutputUserDocuments(Guid qvsId)
        {
            List<DocumentNode> docs = apiClient.GetUserDocuments(qvsId);
            WriteDocumentHeader();
            foreach (DocumentNode dn in docs) WriteDocumentInfo(dn);
        }

        private static void OutputUserDocumentsAccessEntries(Guid qvsId)
        {
            List<DocumentNode> docs = apiClient.GetUserDocuments(qvsId);
            WriteDocumentAccessEntryHeader();
            foreach (DocumentNode dn in docs)
            {
                DocumentMetaData m = apiClient.GetDocumentMetaData(dn, DocumentMetaDataScope.Authorization);
                foreach (DocumentAccessEntry dae in m.Authorization.Access) WriteUserDocumentAccessEntry(dn, dae);
                //WriteUserDocumentAccessEntry(dn.Name, dn.ID, m.Authorization.Access[0].UserName, m.Authorization.Access[0]
            }
        }

        private static void WriteTaskInfo(TaskInfo ti)
        {
            DocumentTask dt = apiClient.GetDocumentTask(ti.ID, DocumentTaskScope.Triggering);
            
            string startAt = "";
            string hourly = "", daily = "", weekly = "", monthly = "";
            foreach (Trigger t in dt.Triggering.Triggers)
            {
                try
                {
                    startAt = ((RecurrenceTrigger)t).StartAt.ToString("HH:mm:ss");
                    hourly = ((RecurrenceTrigger)t).Hourly != null ? ((RecurrenceTrigger)t).Hourly.RecurEvery.ToString() : "N/A";
                    daily = ((RecurrenceTrigger)t).Daily != null ? ((RecurrenceTrigger)t).Daily.RecurEvery.ToString() : "N/A";
                    weekly = ((RecurrenceTrigger)t).Weekly != null ? ((RecurrenceTrigger)t).Weekly.RecurEvery.ToString() : "N/A";
                    //monthly = ((RecurrenceTrigger)t).Monthly.DayConstraints.ToString();
                }
                catch (System.Exception)
                {
                }

                Console.WriteLine(ti.Name + "\t" +
                    apiClient.GetTaskStatus(ti.ID, TaskStatusScope.All).General.Status.ToString() + "\t" +
                    ti.Enabled.ToString() + "\t" + ti.ID.ToString() + "\t" + startAt + "\t" + hourly
                        + "\t" + daily + "\t" + weekly);
            }
        }

        private static void WriteTaskHeader()
        {
            Console.WriteLine("Name\tStatus\tEnabled\tID\tStartAt\thourly\tdaily\tweekly");
        }

        private static void WriteDocumentHeader()
        {
            Console.WriteLine("Name\tTaskCount\tRelativePath\tID");
        }

        private static void WriteDocumentAccessEntryHeader()
        {
            Console.WriteLine("DocName\tDocID\tUserName\tAccessMode");
        }

        private static void WriteDocumentInfo(DocumentNode dn)
        {
            Console.WriteLine(dn.Name + "\t" + dn.TaskCount.ToString() + "\t" + dn.RelativePath + "\t" + dn.ID);
        }

        private static void WriteUserDocumentAccessEntry(DocumentNode dn, DocumentAccessEntry dae)
        {
            Console.WriteLine(dn.Name + "\t" + dn.ID.ToString() + "\t" + dae.UserName + "\t" + dae.AccessMode.ToString());
        }
    }
}
