//-----------------------------------------------------------------------
// <copyright file="GTAC_TcUI_PostgreSQL.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------
// Edits by b.lekx-toniolo to create custom Server Extension
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;

using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Tools.Management;
using Npgsql;


namespace TcHmi_PostgreSQL
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmi_PostgreSQL : IServerExtension
    {
        private readonly RequestListener _requestListener = new RequestListener();

        //Some connection paramters
        private readonly string connTimeout = "2";
        private readonly string cmdTimeout = "3";
        private readonly string keepAlive = "10";


        //Create Npgsql connection object and connected flag place holders
        private NpgsqlConnection DB_Connection;
        private bool DB_isconnected;

        //Some internal variables
        private string rQUERY;
        private string wCOMMAND;



        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            _requestListener.OnRequest += OnRequest;

            return ErrorValue.HMI_SUCCESS;
        }

        //-----------------------------------------------------------------
        // Custom Methods for use in this Server Extension, b.lekx-toniolo
        //-----------------------------------------------------------------

        //------------------------------------------------------
        //------------ Connect to DB Base Method----------------
        //------------------------------------------------------

        private void CONNECT(Command command)
        {

            //If no connection, create new connection and open 
            if (DB_isconnected != true || DB_Connection.FullState.ToString() != "Open")
            {
                //Retreive Server Extension parameters
                string ServerAddr = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "AddrServer");
                string Port = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
                string DB = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Database");
                string username = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
                //DEBUG must encrypt at entry point
                string password = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "userpassword");


                //------ NPGSQL connection --------

                //Build connection string using parameters from TcHmi Server Configuration
                string connectionString =
                    "Host=" + ServerAddr + ";Port = " + Port + ";Database=" + DB + ";Username =" + username + ";Password=" + password + ";Timeout=" + connTimeout + ";CommandTimeout=" + cmdTimeout + ";Keepalive=" + keepAlive + ";";


                //Assemble connection
                DB_Connection = new NpgsqlConnection(connectionString);

                //Set some parameters

                //Try and open a connection, catch exceptions and respond as required
                try
                {
                    DB_Connection.Open();
                    command.ReadValue = DB_Connection.FullState.ToString() + " (" + DB_Connection.Database + ")";
                    command.ExtensionResult = TcHmi_PostgreSQLErrorValue.TcHmi_PostgreSQLSuccess;
                    DB_isconnected = true;
                }
                catch (Exception e)
                {
                    command.ReadValue = "No Connection (" + e.Message + ")";
                    command.ResultString = "N/A";
                    DB_isconnected = false;
                }

            }
            //Connection is already established so simply confirm and pass data
            else
            {
                command.ReadValue = DB_Connection.FullState.ToString() + " (" + DB_Connection.Database + ")";
                command.ExtensionResult = TcHmi_PostgreSQLErrorValue.TcHmi_PostgreSQLSuccess;
                DB_isconnected = true;
            }
        }
               

        //Method calls from the server extension Interface (TcHmi symbol triggers)

        //--------------------------------------------------------
        //------------ Query (Read) Method --------------
        //--------------------------------------------------------

        private void READ(Command command)
        {

            if (DB_Connection.State.ToString() == "Open")
            {

                //If SQL command is sent via the Read Trigger then capture and overwrite the manually set rQUERY variable
                if (command.WriteValue != null)
                {
                    rQUERY = command.WriteValue;
                }

                //Ensure rQUERY has something (either sent from command i/f or from manually set via setQUERY        
                if (rQUERY != null)
                {
                    //Create a new Npgsql command
                    var SQLreadcommand = new NpgsqlCommand(rQUERY, DB_Connection);

                    try
                    {
                        //Create newe Data Read Object
                        using NpgsqlDataReader DBreaderObject = SQLreadcommand.ExecuteReader();
                        while (DBreaderObject.Read())
                        {
                            //Return column zero of the SELECT command (future versions will allow multiple column returns
                            command.ReadValue = DBreaderObject.GetValue(0).ToString();
                            //Clear out previous Query string
                            rQUERY = "";
                        }
                      
                    }
                    catch (Exception e)
                    {
                        command.ReadValue = "Failed to read: " + e.Message;
                    }
                    
                    //Final Resource Clean-up / Garbage collection
                    SQLreadcommand.Dispose();

                }
                else
                {
                    command.ReadValue = "QUERY string null, either set READ symbol upon trigger or use setQUERY method before triggering READ";
                }
            }
            else
            {
                command.ReadValue = "Not connected to DB";
            }
        }

 
        //------------------------------------------------------------
        //------------- Write command to DB Method-----------------
        //------------------------------------------------------------
        private void WRITE(Command command)
        {   
            if (DB_Connection.State.ToString() == "Open")
            {
                //If SQL command is sent via the WRITETrigger then capture and overwrite the manually set wINSERT variable
                if (command.WriteValue != null)
                {
                    wCOMMAND = command.WriteValue;
                }

                //Ensure wINSERT has something (either sent from command i/f or from manually set via setINSERT        
                if (wCOMMAND != null)
                {
                    //Create a new Npgsql Command
                    var SQLwritecommand = new NpgsqlCommand(wCOMMAND, DB_Connection);

                    try
                    {
                        SQLwritecommand.ExecuteNonQuery();
                        command.ReadValue = "Command Executed";
                        //Clear out previous Insert / Command string
                        wCOMMAND = "";
                    }

                    catch (Exception e)
                    {
                        command.ReadValue = "Failed to write:" + e.Message;

                    }

                    //Final Resource Clean-up / Garbage collection
                    SQLwritecommand.Dispose();
                }
                else
                {
                    command.ReadValue = "COMMAND string null, either set WRITE symbol upon trigger or use setCOMMAND method before triggering WRITE";
                }
            }
            else
            {
                command.ReadValue = "Not connected to DB";
            }
        }
        //--------------------------------------------
        //------------- Close Connection -------------
        //--------------------------------------------
        private void CLOSE(Command command)
        {
            DB_Connection.Close();
            DB_Connection.Dispose();
            DB_isconnected = false;
        }
        //------------------------------------------------------
        //------------------ Gettter Status Method -------------
        //------------------------------------------------------

 
        //------------- Get Connected Status of DB-------------
        private void getCONNECTED(Command command)
        {
            command.ReadValue = DB_isconnected;
        }


        //------------- Get Current Query String -------------
        private void getQUERY(Command command)
        {
            command.ReadValue = "Current Query String: "+ rQUERY;
        }

        //------------- Get Current Command String -------------
        private void getCOMMAND(Command command)
        {
            command.ReadValue = "Current Command String: " + wCOMMAND;
        }
        //-----------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------


        //------------------------------------------------------
        //------------------ Setter Method ---------------------
        //------------------------------------------------------

        //------------- Set Query String -------------
        private void setQUERY(Command command)
        {
            rQUERY = command.WriteValue;
        }
        
        //------------- Set Comand String -------------
        private void setCOMMAND(Command command)
        {
            wCOMMAND = command.WriteValue;
        }


        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        //------------- TcHmi Server Extension Interface -------------------------
        //---------- (commands from TcHmi that end up calling the above)----------
        //------------------------------------------------------------------------

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void OnRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            try
            {
                e.Commands.Result = TcHmi_PostgreSQLErrorValue.TcHmi_PostgreSQLSuccess;

                foreach (Command command in e.Commands)
                {
                    try
                    {
                        // Use the mapping to check which command is requested
                        switch (command.Mapping)
                        {
                            
                            //-------- Functional Method calls ---------    
                            //Connect to DB Server
                            case "CONNECT":
                                CONNECT(command);
                                break;
                            
                            //Read Data from Database
                            case "READ":
                                READ(command);
                                break;

                            //Write Command to Database
                            case "WRITE":
                                WRITE(command);
                                break;

                            //Close Conection
                            case "CLOSE":
                                CLOSE(command);
                                break;
                            //-------- Getter Method Calls ------

                            //Get Connected Status Primary DB
                            case "getCONNECTED":
                                getCONNECTED(command);
                                break;

                            //Get Current Query string, Primary DB
                            case "getQUERY":
                                getQUERY(command);
                                break;

                            //Get Current Insert string, Primary DB
                            case "getCOMMAND":
                                getCOMMAND(command);
                                break;

                            //-------- Setter Method Calls ------

                            //Set QUERY STRING for DB Reads
                            //Primary DB
                            case "setQUERY":
                                setQUERY(command);
                                break;

                            //Set INSERT STRING for DB Writes
                            //Primary DB
                            case "setCOMMAND":
                                setCOMMAND(command);
                                break;

                            //Default case
                            default:
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new TcHmiException("GTAC_TcUI_PostgreSQL TcHmi Error ->"+ex.Message.ToString(), ErrorValue.HMI_E_EXTENSION);
            }
        }
    }
}
