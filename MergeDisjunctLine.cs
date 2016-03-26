using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;

namespace GPMergeDisconnectLine
{     
    //定义GP工具
    public class MergeDisjunctLineFunction : IGPFunction
    {
        private string m_ToolName = "MergeDisconnectLine";
        private string m_metadatafile = "MergeDisconnectLine.xml";
        private IArray m_Parameters;             // Array of Parameters
        private IGPUtilities m_GPUtilities;      

        public MergeDisjunctLineFunction()
        {
            m_GPUtilities = new GPUtilitiesClass();
        }

        public string Name
        {
            get { return m_ToolName; }
        }

        // Set the function tool Display Name as seen in ArcToolbox.
        public string DisplayName
        {
            get { return "Merge Disconnect Line"; }
        }

        public void Execute(IArray paramvalues, ITrackCancel trackcancel, IGPEnvironmentManager envMgr, IGPMessages message)
        {
            IGPParameter parameter = (IGPParameter)paramvalues.get_Element(0);
            IGPValue parameterValue = m_GPUtilities.UnpackGPValue(parameter);
            // Open Input Dataset
            IFeatureClass inputFeatureClass = null;
            IQueryFilter qf = null;
            IGPRecordSet gprs = null;
            IRecordSet2 rs2 = null;

            IFeatureClass outputFeatureClass = null;
            //ICursor SearchCursor = null;
            int sumCount = 0;
            if (parameterValue.DataType is IDEGeoDatasetType)
            {
                m_GPUtilities.DecodeFeatureLayer(parameterValue, out inputFeatureClass, out qf);
                if (inputFeatureClass == null)
                {
                    message.AddError(2, "Could not open input dataset.");
                    return;
                }
                //SearchCursor = inputFeatureClass.Search(null, false) as ICursor;
                sumCount = inputFeatureClass.FeatureCount(null);
            }
            else if (parameterValue.DataType is IGPFeatureRecordSetLayerType)
            {
                gprs = parameterValue as IGPRecordSet;
                rs2 = gprs.RecordSet as IRecordSet2;
                //SearchCursor = rs2.get_Cursor(false);
                sumCount = rs2.Table.RowCount(null);
            }

            /*Create FeatureClass*/
            parameter = (IGPParameter)paramvalues.get_Element(1);
            parameterValue = m_GPUtilities.UnpackGPValue(parameter);
            Geoprocessor gp = new Geoprocessor();
            // Create the new Output Polyline Feature Class
            CreateFeatureclass cfc = new CreateFeatureclass();      
            IName name = m_GPUtilities.CreateFeatureClassName(parameterValue.GetAsText());
            IDatasetName dsName = name as IDatasetName;
            IFeatureClassName fcName = dsName as IFeatureClassName;
            IFeatureDatasetName fdsName = fcName.FeatureDatasetName as IFeatureDatasetName;

            // Check if output is in a FeatureDataset or not. Set the output path parameter for CreateFeatureClass tool.
            if (fdsName != null)
            {
                cfc.out_path = fdsName;
            }
            else
            {
                cfc.out_path = dsName.WorkspaceName.PathName;
            }
            // Set the output Coordinate System for CreateFeatureClass tool.
            IGPEnvironment env = envMgr.FindEnvironment("outputCoordinateSystem");
            if (env.Value.IsEmpty())
            {
                //IGeoDataset ds = inputFeatureClass as IGeoDataset;
                //cfc.spatial_reference = ds.SpatialReference as ISpatialReference3;
            }
            else
            {
                IGPCoordinateSystem cs = env.Value as IGPCoordinateSystem;
                cfc.spatial_reference = cs.SpatialReference as ISpatialReference3;
            }
            // Remaing properties for Create Feature Class Tool
            cfc.out_name = dsName.Name;
            cfc.geometry_type = "POLYLINE";
            gp.Execute(cfc, null);
            outputFeatureClass = m_GPUtilities.OpenFeatureClassFromString(parameterValue.GetAsText());
            //Set the properties of the Step Progressor
            IStepProgressor pStepPro = (IStepProgressor)trackcancel;
            pStepPro.MinRange = 0;
            pStepPro.MaxRange = 6;
            pStepPro.StepValue = (1);
            pStepPro.Message = "Merge disjunct polyline is in processing";
            pStepPro.Position = 0;
            pStepPro.Show();

            MergeOperation mOpetation = new MergeOperation();
            pStepPro.Step();
            List<IFeature> allPolylineList = mOpetation.getAllPolyline(inputFeatureClass);
            pStepPro.Step();
            List<IPoint> allNodePointList = mOpetation.GetNodePtsListByLine(allPolylineList);
            pStepPro.Step();
            List<IPoint> distinctNodePointList = mOpetation.GetDistinctNodePtsList(allNodePointList);
            pStepPro.Step();
            List<IFeature> unionLineList = mOpetation.MergeLineListOperate(allPolylineList, distinctNodePointList, inputFeatureClass);
            pStepPro.Step();
            mOpetation.AddField(inputFeatureClass, outputFeatureClass);
            pStepPro.Step();
            mOpetation.WriteUnionLineToFile(unionLineList, outputFeatureClass);
            pStepPro.Step();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(outputFeatureClass);
            
            pStepPro.Hide();
        }

        // This is the location where the parameters to the Function Tool are defined. 
        // This property returns an IArray of parameter objects (IGPParameter). 
        // These objects define the characteristics of the input and output parameters. 
        public IArray ParameterInfo
        {                 
            get 
            {
                //Array to the hold the parameters	
                IArray parameters = new ArrayClass();

                IGPParameterEdit3 inputParameter = new GPParameterClass();
                inputParameter.DataType = new GPFeatureLayerTypeClass();
                inputParameter.Value = new GPFeatureLayerClass();

                // Set Input Parameter properties
                inputParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputParameter.DisplayName = "Input Features";
                inputParameter.Name = "input_features";
                inputParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                parameters.Add(inputParameter);

                // Output parameter (Derived) and data type is DEFeatureClass
                IGPParameterEdit3 outputParameter = new GPParameterClass();
                outputParameter.DataType = new DEFeatureClassTypeClass();

                // Value object is DEFeatureClass
                outputParameter.Value = new DEFeatureClassClass();

                // Set output parameter properties
                outputParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputParameter.DisplayName = "Output FeatureClass";
                outputParameter.Name = "out_featureclass";
                outputParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                // Create a new schema object - schema means the structure or design of the feature class (field information, geometry information, extent)
                IGPFeatureSchema outputSchema = new GPFeatureSchemaClass();
                IGPSchema schema = (IGPSchema)outputSchema;

                // Clone the schema from the dependency. 
                //This means update the output with the same schema as the input feature class (the dependency).                                
                schema.CloneDependency = true;

                // Set the schema on the output because this tool will add an additional field.
                outputParameter.Schema = outputSchema as IGPSchema;
                outputParameter.AddDependency("input_features");
                parameters.Add(outputParameter);

                return parameters;
            }
        }

        //验证合法性
        public IGPMessages Validate(IArray paramvalues, bool updateValues, IGPEnvironmentManager envMgr)
        {
            if (m_Parameters == null)
                m_Parameters = ParameterInfo;
            if (updateValues)
            {
                //UpdateParameters(paramvalues, envMgr);
            }
            //// Call InternalValidate (Basic Validation). Are all the required parameters supplied?
            //// Are the Values to the parameters the correct data type?
            IGPMessages validateMsgs = m_GPUtilities.InternalValidate(m_Parameters, paramvalues, updateValues, true, envMgr);
            return validateMsgs;
        }


        public void UpdateParameters(IArray paramvalues, IGPEnvironmentManager pEnvMgr)
        {
            //m_Parameters = paramvalues;
            // Retrieve the input parameter value
            //IGPValue parameterValue = m_GPUtilities.UnpackGPValue(m_Parameters.get_Element(0));
        }
        
        // This is the function name object for the Geoprocessing Function Tool. 
        // This name object is created and returned by the Function Factory.
        // The Function Factory must first be created before implementing this property.
        public IName FullName
        {
            get
            { 	
                IGPFunctionFactory functionFactory = new MergeDisjunctLineFactory();
                return (IName)functionFactory.GetFunctionName(m_ToolName);
            }
        }

        // This is used to set a custom renderer for the output of the Function Tool.
        public object GetRenderer(IGPParameter pParam)
        {
            return null;
        }

        //帮助的上下文标识 返回0即可 
        public int HelpContext
        {
            get { return 0; }
        }

        // This is the path to a .chm file which is used to describe and explain the function and its operation. 
        public string HelpFile
        {
            get { return ""; }
        }

        // This is used to return whether the function tool is licensed to execute.
        //验证许可  
        public bool IsLicensed()
        {
            IAoInitialize aoi = new AoInitializeClass();
            ILicenseInformation licInfo = (ILicenseInformation)aoi;
            string licName = licInfo.GetLicenseProductName(aoi.InitializedProduct());
            if (licName == "Advanced")
            {
                return true;
            }
            else
            {
                return false;
            }           
        }

        //元数据文件 这个返回空字符串也可以 
        public string MetadataFile
        {
            get
            {
                string filePath;
                filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                filePath = System.IO.Path.Combine(filePath, m_metadatafile);
                return filePath;
            }
        }

        public UID DialogCLSID
        {
            // DO NOT USE. INTERNAL USE ONLY.
            get { return null; }
        }
    }


    /*IGPFunctionFactory*************************************************************************************************/

    [Guid("526de91e-3fe5-4a46-a7e2-4d1dc3cdb5db"), ComVisible(true)]

    public class MergeDisjunctLineFactory : IGPFunctionFactory
    {
        // Register the Function Factory with the ESRI Geoprocessor Function Factory Component Category.

        #region "Component Category Registration"

        [ComRegisterFunction()]
        private static void Reg(string regKey)
        {
            GPFunctionFactories.Register(regKey);
        }

        [ComUnregisterFunction()]
        private static void Unreg(string regKey)
        {
            GPFunctionFactories.Unregister(regKey);
        }

        #endregion

        // Utility Function added to create the function names.
        private IGPFunctionName CreateGPFunctionNames(long index)
        {
            IGPFunctionName functionName = new GPFunctionNameClass();
            functionName.MinimumProduct = esriProductCode.esriProductCodeAdvanced;
            IGPName name;

            switch (index)
            {
                case (0):
                    name = (IGPName) functionName;
                    name.Category = "DisconnectlineMerge";
                    name.Description = "Merge a disconnect line list to a continuous line";
                    name.DisplayName = "Merge Disconnectline";
                    name.Name = "MergeDisconnectLine";
                    name.Factory = (IGPFunctionFactory) this;
                    break;
            }
            return functionName;
        }

        // Implementation of the Function Factory
        // This is the name of the function factory. 
        // This is used when generating the Toolbox containing the function tools of the factory.
        public string Name
        {
            get { return "DisconnectlineMerge"; }
        }

        // This is the alias name of the factory.
        public string Alias
        {
            get { return "lineMerge"; }
        }

        // This is the class id of the factory. 
        public UID CLSID
        {
            get
            {
                UID id = new UIDClass();
                id.Value = this.GetType().GUID.ToString("B");
                return id;
            }
        }

        // This method will create and return a function object based upon the input name.
        public IGPFunction GetFunction(string Name)
        {
            switch (Name)
            {
                case ("MergeDisconnectLine"):
                    IGPFunction gpFunction = new MergeDisjunctLineFunction();
                    return gpFunction;
            }

            return null;
        }

        // This method will create and return a function name object based upon the input name.
        public IGPName GetFunctionName(string Name)
        {
            IGPName gpName = new GPFunctionNameClass();

            switch (Name)
            {
                case ("MergeDisconnectLine"):
                    return (IGPName) CreateGPFunctionNames(0);

            }
            return null;
        }

        // This method will create and return an enumeration of function names that the factory supports.
        public IEnumGPName GetFunctionNames()
        {
            IArray nameArray = new EnumGPNameClass();
            nameArray.Add(CreateGPFunctionNames(0));
            return (IEnumGPName) nameArray;
        }

        public IEnumGPEnvironment GetFunctionEnvironments()
        {
            return null;
        }
    }
}
