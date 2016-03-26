using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace GPMergeDisconnectLine
{
    class MergeOperation
    {
        public MergeOperation ()
        {
            //构造函数
        }

        #region 线要素合并处理函数
        List<IFeature> DisconnPolylineList = new List<IFeature>();
        List<IFeature> firstRowFeatureList = new List<IFeature>();
        
        //获取shp文件中所有的Polyline(IFeature)对象
        public List<IFeature> getAllPolyline(IFeatureClass inputFeatureClass)
        {
            IQueryFilter queryFilter = new QueryFilter();
            queryFilter.WhereClause = "";
            IFeatureCursor pFeatCursor = inputFeatureClass.Search(queryFilter, false);
            IFeature pFeature = pFeatCursor.NextFeature();

            while (pFeature != null)
            {
                if (inputFeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                {
                    DisconnPolylineList.Add(pFeature);
                }
                pFeature = pFeatCursor.NextFeature();
            }
            return DisconnPolylineList;
        }

        //通过遍历线要素首尾点的坐标的形式找到节点
        public List<IPoint> GetNodePtsListByLine(List<IFeature> allPolyline)
        {
            List<IPoint> nodePtsList = new List<IPoint>();
            for (int j = 0; j < allPolyline.Count; j++)
            {
                IGeometry firstGeometry = allPolyline[j].Shape;
                IPolyline firstLine = firstGeometry as IPolyline;
                IPoint startPt1 = firstLine.FromPoint;
                IPoint endPt1 = firstLine.ToPoint;
                int fromFlag = 0;
                int toFlag = 0;
                for (int i = 0; i < allPolyline.Count; i++)
                {
                    IGeometry secondGeometry = allPolyline[i].Shape;
                    IPolyline secondLine = secondGeometry as IPolyline;
                    IPoint startPt2 = secondLine.FromPoint;
                    IPoint endPt2 = secondLine.ToPoint;
                    //FromPoint相同的点
                    if ((startPt1.X == startPt2.X && startPt1.Y == startPt2.Y) ||
                        (startPt1.X == endPt2.X && startPt1.Y == endPt2.Y))
                    {
                        fromFlag++;
                    }
                    //ToPoint相同的点
                    if ((endPt1.X == endPt2.X && endPt1.Y == endPt2.Y) ||
                    (endPt1.X == startPt2.X && endPt1.Y == startPt2.Y))
                    {
                        toFlag++;
                    }
                }
                if (fromFlag > 2)
                {
                    nodePtsList.Add(startPt1);
                }
                if (toFlag > 2)
                {
                    nodePtsList.Add(endPt1);
                }
            }
            return nodePtsList;
        }
        //去除List集合中重复的节点
        public List<IPoint> GetDistinctNodePtsList(List<IPoint> nodePtsList)
        {
            for (int i = 0; i < nodePtsList.Count; i++)
            {
                for (int j = nodePtsList.Count - 1; j > i; j--)
                {
                    if (nodePtsList[i].X == nodePtsList[j].X && nodePtsList[i].Y == nodePtsList[j].Y)
                    {
                        nodePtsList.RemoveAt(j);
                    }
                }
            }
            return nodePtsList;
        }

        //获取所有的线要素点
        public List<IPoint> GetPointsList(List<IFeature> disconnPolylineList)
        {
            List<IPoint> PointsList = new List<IPoint>();
            IPointCollection PtsColl = disconnPolylineList[0] as IPointCollection;
            for (int i = 1; i < disconnPolylineList.Count; i++)
            {
                IPointCollection linePts = disconnPolylineList[i] as IPointCollection;
                PtsColl.AddPointCollection(linePts);
            }
            for (int i = 0; i < PtsColl.PointCount; i++)
            {
                PointsList.Add(PtsColl.get_Point(i));
            }
            return PointsList;
        }

        //将需要进行合并的线要素（没有节点）集合进行合并，结果为多条线        
        public List<IFeature> MergeLineListOperate(List<IFeature> toUnionLineList, List<IPoint> distinctNodePointList, IFeatureClass inputFeatureClass)
        {
            List<IFeature> mergeResultLineList = new List<IFeature>();
            int CountPercent = 0;
            while (toUnionLineList.Count > 0)
            {
                CountPercent++;
                IFeature unionCurrentLine = toUnionLineList[0];
                firstRowFeatureList.Add(unionCurrentLine);
                List<IFeature> currentMergeLineList = new List<IFeature>();
                int count2 = 0;
                do
                {
                    count2++;
                    bool needLink1 = true;
                    bool needLink2 = true;
                    IFeature featureFirst = unionCurrentLine;
                    IGeometry geoLineFirst = featureFirst.Shape;
                    IPolyline lineFirst = geoLineFirst as IPolyline;

                    IPoint startPt1 = lineFirst.FromPoint;
                    IPoint endPt1 = lineFirst.ToPoint;
                    foreach (IPoint pt in distinctNodePointList)
                    {
                        if (pt.X == startPt1.X && pt.Y == startPt1.Y)
                        {
                            needLink1 = false;
                        }
                        if (pt.X == endPt1.X && pt.Y == endPt1.Y)
                        {
                            needLink2 = false;
                        }
                    }
                    toUnionLineList.Remove(featureFirst);
                    currentMergeLineList.Clear();
                    currentMergeLineList.Add(featureFirst);

                    List<IFeature> allPolylineListTemp1 = new List<IFeature>();
                    List<IFeature> allPolylineListTemp2 = new List<IFeature>();
                    int bStart1 = 0;
                    int bStart2 = 0;

                    for (int j = 0; j < toUnionLineList.Count; j++)
                    {
                        IFeature featureSecond = toUnionLineList[j];
                        IGeometry geoLineSecond = featureSecond.Shape;
                        IPolyline lineSecond = geoLineSecond as IPolyline;
                        IPoint startPt2 = lineSecond.FromPoint;
                        IPoint endPt2 = lineSecond.ToPoint;

                        if (needLink1 && ((startPt1.X == startPt2.X && startPt1.Y == startPt2.Y) ||
                            (startPt1.X == endPt2.X && startPt1.Y == endPt2.Y)))
                        {
                            bStart1++;
                            if (bStart1 > 0)
                            {
                                allPolylineListTemp1.Add(featureSecond);
                                currentMergeLineList.AddRange(allPolylineListTemp1);
                                toUnionLineList.Remove(featureSecond);
                            }
                        }
                        if (needLink2 && ((endPt1.X == endPt2.X && endPt1.Y == endPt2.Y) ||
                            (endPt1.X == startPt2.X && endPt1.Y == startPt2.Y)))
                        {
                            bStart2++;
                            if (bStart2 > 0)
                            {
                                allPolylineListTemp2.Add(featureSecond);
                                currentMergeLineList.AddRange(allPolylineListTemp2);
                                toUnionLineList.Remove(featureSecond);
                            }
                        }
                    }
                    if (currentMergeLineList.Count > 1)
                    {
                        unionCurrentLine = UnionCurrentLineList(currentMergeLineList, inputFeatureClass);
                    }
                    else
                    {
                        int ii = 0;
                    }
                } while (currentMergeLineList.Count > 1);
                mergeResultLineList.Add(unionCurrentLine);
            }
            return mergeResultLineList;
        }

        //为待写入图层添加和原图层相同的字段
        public void AddField(IFeatureClass inputFeatureClass, IFeatureClass outputFeatureClass)
        {
            try
            {
                IClass newClass = outputFeatureClass as IClass;

                IFields sourceFields = inputFeatureClass.Fields;
                for (int i = 2; i < sourceFields.FieldCount - 1; i++)
                {
                    IField fieldTemp = new Field();
                    IFieldEdit2 fieldEdit = fieldTemp as IFieldEdit2;
                    fieldEdit.Type_2 = sourceFields.Field[i].Type;
                    fieldEdit.Name_2 = sourceFields.Field[i].Name;
                    newClass.AddField(fieldEdit);
                }
            }
            catch
            {
                MessageBox.Show("为新的图层要素添加和源图层相同的字段时出错！");
            }

        }

        public void WriteUnionLineToFile(List<IFeature> mergeResultLineList, IFeatureClass outputFeatureClass)
        {
            try
            {
                int index = 0;
                foreach (IFeature featureLine in mergeResultLineList)
                {
                    IFeatureBuffer featureBuffer = outputFeatureClass.CreateFeatureBuffer();
                    IFeatureCursor featureCursor;
                    featureCursor = outputFeatureClass.Insert(true);
                    IGeometry pGeometry = featureLine.Shape;
                    featureBuffer.Shape = pGeometry;

                    for (int i = 3; i < featureBuffer.Fields.FieldCount; i++)
                    {
                        string fieldName = featureBuffer.Fields.Field[i].Name;
                        int indexTemp = firstRowFeatureList[index].Fields.FindField(fieldName);
                        if (indexTemp >= 0)
                        {
                            featureBuffer.set_Value(i, firstRowFeatureList[index].get_Value(indexTemp));
                        }
                    }
                    featureCursor.InsertFeature(featureBuffer);
                    featureCursor.Flush();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);
                    index++;
                }
            }
            catch
            {
                MessageBox.Show("合并线要素属性时出错！");
            }

        }

        //将传入的List<IPolylne>中的多条线要素进行合并为一条线要素
        public IFeature UnionCurrentLineList(List<IFeature> currentMergeLineList, IFeatureClass inputFeatureClass)
        {
            ITopologicalOperator2 pTopologicalOperator;
            IFeature pFeatureTemp = currentMergeLineList[0];
            IGeometry pGeometry = pFeatureTemp.Shape;
            int i = 1;
            while (i < currentMergeLineList.Count)
            {
                pTopologicalOperator = pGeometry as ITopologicalOperator2;
                pTopologicalOperator.IsKnownSimple_2 = false;
                pTopologicalOperator.Simplify();
                pGeometry.SnapToSpatialReference();

                pGeometry = currentMergeLineList[i].Shape;
                pGeometry = pTopologicalOperator.Union(pGeometry);
                i++;
            }
            IFeature unionLine = inputFeatureClass.CreateFeature();
            unionLine.Shape = pGeometry;
            IDataset pDataset = inputFeatureClass as IDataset;
            pDataset.Workspace.ExecuteSQL("delete from " + inputFeatureClass.AliasName + " where SHAPE_Length = 0");
            return unionLine;
        }
        #endregion
    }
}
