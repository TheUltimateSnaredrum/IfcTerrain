﻿using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
//using BimGisCad.Collections; //removed [MESH]
//implementing TIN instead of MESH
using BimGisCad.Representation.Geometry.Composed;
using BimGisCad.Representation.Geometry.Elementary;
using NLog;

namespace IFCTerrain.Model.Read
{
    public static class CityGml
    {
        /// <summary>
        ///  Liest das erste TIN aus einer GML Datei
        /// </summary>
        /// <param name="fileName">  </param>
        /// <returns>  </returns>
        public static Result ReadTIN(bool is3d, string fileName, double minDist, string logFilePath, string verbosityLevel)
        {
            var logger = LogManager.GetCurrentClassLogger();

            //TIN-Builder
            var tinB = Tin.CreateBuilder(true);
            int pnr = 0;

            var result = new Result();
            try
            {
                using(var reader = XmlReader.Create(fileName))
                {
                    bool isRelief = false;
                    reader.MoveToContent();
                    while(!reader.EOF && (reader.NodeType != XmlNodeType.Element || !(isRelief = reader.LocalName == "ReliefFeature")))
                    { reader.Read(); }
                    if(isRelief)
                    {
                        string id = reader.MoveToFirstAttribute() && reader.LocalName == "id" ? reader.Value : null;
                        bool insideTin = false;
                        while(!reader.EOF && (reader.NodeType != XmlNodeType.Element || !(insideTin = reader.LocalName == "tin")))
                        { reader.Read(); }
                        if(insideTin)
                        {
                            bool insideTri = false;
                            while(!reader.EOF && (reader.NodeType != XmlNodeType.Element || !(insideTri = reader.LocalName == "trianglePatches")))
                            { reader.Read(); }
                            if(insideTri)
                            {
                                while(!reader.EOF && (reader.NodeType != XmlNodeType.Element || !(insideTri = reader.LocalName == "Triangle")))
                                { reader.Read(); }
                                if(insideTri)
                                {
                                    //var tin = new Mesh(is3d, minDist);
                                    while(reader.NodeType == XmlNodeType.Element && reader.LocalName == "Triangle"
                                        && XElement.ReadFrom(reader) is XElement el)
                                    {
                                        var posList = el.Descendants().Where(d => d.Name.LocalName == "posList" && !d.IsEmpty);
                                        string[] pl;
                                        if(posList.Any()
                                            && (pl = posList.First().Value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Length == 12
                                            && pl[0] == pl[9] && pl[1] == pl[10] && pl[2] == pl[11]
                                            && Point3.Create(pl, out var pt1)
                                            && Point3.Create(pl, out var pt2, 3)
                                            && Point3.Create(pl, out var pt3, 6))
                                        {
                                            //first Add points to tin with point index (pnr)
                                            tinB.AddPoint(pnr++, pt1);
                                            tinB.AddPoint(pnr++, pt2);
                                            tinB.AddPoint(pnr++, pt3);

                                            //adding Triangle to TIN-Builder
                                            tinB.AddTriangle(pnr - 3, pnr - 2, pnr - 1, true);
                                            
                                            //tin.AddFace(new[] { pt1, pt2, pt3 });
                                        }
                                        reader.Read();
                                    }
                                    /*
                                    if(!tin.Points.Any() || !tin.FaceEdges.Any())
                                    {
                                        result.Error = string.Format(Properties.Resources.errNoTINData, Path.GetFileName(fileName));
                                        logger.Error("No TIN-data found");
                                        return result;
                                    }
                                    */
                                    //result.Mesh = tin;
                                    logger.Info("Reading GML-data successful");
                                    logger.Info(result.Tin.Points.Count() + " points; " + result.Tin.NumTriangles + " triangels processed");

                                    //TIN aus TIN-Builder erzeugen
                                    Tin tin = tinB.ToTin(out var pointIndex2NumberMap, out var triangleIndex2NumberMap);
                                    //Result beschreiben
                                    result.Tin = tin;
                                    //Result übergeben
                                    return result;
                                }
                            }
                        }
                    }
                    result.Error = string.Format(Properties.Resources.errNoTIN, Path.GetFileName(fileName));
                    logger.Error("No TIN-data found");
                    return result;
                }
            }
            catch
            {
                result.Error = string.Format(Properties.Resources.errFileNotReadable, Path.GetFileName(fileName));
                logger.Error("File not readable");
                return result;
            }
        } //End ReadTIN
    }
}