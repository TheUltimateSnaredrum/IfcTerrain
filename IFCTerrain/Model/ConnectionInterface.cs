﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using IFCTerrain.Model.Read;
using IFCTerrain.Model.Read.GEOgraf;
using IFCTerrain.Model.Write;
using IxMilia.Dxf;
using BimGisCad.Collections;

using Xbim.Ifc2x3.MeasureResource;
using BimGisCad.Representation.Geometry;
using System.Windows.Forms;
using BimGisCad.Representation.Geometry.Elementary;
using BimGisCad.Representation.Geometry.Composed;
//NLog for creating Log-File
using NLog;

namespace IFCTerrain.Model
{
    public class ConnectionInterface
    {
        public Mesh Mesh { get; private set; } = null;

        public Tin Tin { get; private set; }
        IReadOnlyDictionary<int, int> pointIndex2NumberMap;
        IReadOnlyDictionary<int, int> triangleIndex2NumberMap;
        public Dictionary<int,Line3> Breaklines { get; private set; } = null;

        public Mesh Mesh_Poly { get; private set; } = null; 
        public ReadInput Input { get; private set; } = new ReadInput();
        private DxfFile dxfFile = null;
        private RebDaData rebData = null;

        //siteplacement-argument?

        

        public void mapProcess(JsonSettings jSettings, double? breakDist = null, double? refLatitude = null, double? refLongitude = null, double? refElevation = null)
        {
            
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("----------------------------------------------");

            #region Reader
            //
            //Read
            //

            var result = new Result();

            

            string[] fileNames = new string[1];
            fileNames[0] = jSettings.fileName;

            #region import data typ selection
            switch (jSettings.fileType)
            {
                case "LandXML":
                    result = LandXml.ReadTIN(jSettings.is3D, jSettings.fileName, jSettings.minDist, jSettings.logFilePath, jSettings.verbosityLevel);
                    break;

                case "CityGML":
                    result = CityGml.ReadTIN(jSettings.is3D, jSettings.fileName, jSettings.minDist, jSettings.logFilePath, jSettings.verbosityLevel);
                    break;

                case "DXF":
                    DXF.ReadFile(jSettings.fileName, out dxfFile);

                    if (jSettings.isTin)
                    {
                        //READING as TIN
                        result = DXF.ReadDXFTin(jSettings.is3D, dxfFile, jSettings.layer, jSettings.breakline_layer, jSettings.minDist, jSettings.breakline);
                    }
                    else
                    {
                        //READING as MESH --> have to be changed into TIN
                        result = DXF.ReadDXFIndPoly(jSettings.is3D, dxfFile, jSettings.layer, jSettings.breakline_layer, jSettings.minDist, jSettings.logFilePath, jSettings.verbosityLevel, jSettings.breakline);
                    }
                    break;

                case "REB":
                    this.rebData = RebDa.ReadREB(fileNames);

                    result = RebDa.ConvertReb(jSettings.is3D, this.rebData, jSettings.horizon, jSettings.minDist, jSettings.logFilePath, jSettings.verbosityLevel);
                    break;
                case "Grid":
                    result = ElevationGrid.ReadGrid(jSettings.is3D, jSettings.fileName, jSettings.minDist, jSettings.gridSize, jSettings.bBox, jSettings.bbNorth, jSettings.bbEast, jSettings.bbSouth, jSettings.bbWest);
                    break;
                case "OUT":
                    if (jSettings.isTin)
                    {
                        //result = Out.ReadOUTTin(jSettings.is3D, jSettings.fileName,jSettings.onlyTypes, jSettings.layer, jSettings.minDist, jSettings.ignPos, jSettings.ignHeight, jSettings.onlyHorizon, jSettings.horizonFilter, jSettings.breakline, jSettings.breakline_layer); //reworking

                        result = ReadOUT.ReadOutData(jSettings.fileName, out pointIndex2NumberMap, out triangleIndex2NumberMap); //pointIndex2NumberMap & triangleIndex2NumberMap ggf. entfernen
                    }
                    /* ENTFERNT, da derzeit nur über FACES verarbeitet wird
                    else
                    {
                        
                        //result = Out.ReadOUT_Points_Lines(jSettings.is3D, jSettings.fileName, jSettings.layer, jSettings.minDist, jSettings.ignPos, jSettings.ignHeight, jSettings.breakline, jSettings.breakline_layer);
                    }*/
                    break;
                case "PostGIS":
                    result = PostGIS.ReadPostGIS(jSettings.is3D, jSettings.minDist, jSettings.host, jSettings.port, jSettings.user, jSettings.password, jSettings.database, jSettings.schema, jSettings.tin_table, jSettings.tin_column, jSettings.tinid_column, jSettings.tin_id, jSettings.breakline, jSettings.breakline_table, jSettings.breakline_column, jSettings.breakline_tin_id);
                    break;
            }
            this.Tin = result.Tin;
            this.Mesh = result.Mesh;
            this.Breaklines = result.Breaklines;
           
            #endregion

            #region Mesh-Checker
            if(Mesh != null)
            {
                try
                {
                    logger.Debug("Mesh created with: " + Mesh.Points.Count + " Points; " + Mesh.FixedEdges.Count + " Lines; " + Mesh.FaceEdges.Count + " Faces");
                }
                catch
                {
                    logger.Debug("No Faces or Points found!");
                }
            }
            /*
            else
            {
                
                try
                {
                    //Removed --> relocate in reader
                    //logger.Debug("Tin created with: " + Tin.Points.Count + " Points; " + Tin.NumTriangles + " Triangels");
                }
                catch
                {
                    //logger.Debug("No Triangels or Points found!");
                }
            }
            */
            #endregion

            #endregion

            #region Writer
            //
            //Write
            //

            #region project Name
            if (jSettings.projectName is null)
            {
                jSettings.projectName = "Name of project";
            }
            #endregion
            
            var writeInput = new WriteInput();

            #region Placement

            writeInput.Placement = Axis2Placement3D.Standard;
            if(jSettings.customOrigin)
            {
                writeInput.Placement.Location = Vector3.Create(jSettings.xOrigin, jSettings.yOrigin, jSettings.zOrigin);
            }
            else
            {
                double MinX = 0;
                double MinY = 0;
                double MinZ = 0;
                double MaxX = 0;
                double MaxY = 0;
                double MaxZ = 0;

                if (Mesh != null)
                {
                    double midX = (this.Mesh.MaxX + this.Mesh.MinX) / 2;
                    double midY = (this.Mesh.MaxY + this.Mesh.MinY) / 2;
                    double midZ = (this.Mesh.MaxZ + this.Mesh.MinZ) / 2;

                    writeInput.Placement.Location = Vector3.Create(midX, midY, midZ);
                }
                //Vorschlag für BimGisCad.Composed (in MESH ist dies bereits vorhanden)
                else
                {
                    int i = 0;
                    foreach (Point3 point in Tin.Points)
                    {
                        //initalisierung durch ersten Punkt
                        if (i > 0)
                        {
                            if (point.X < MinX) { MinX = point.X; }
                            if (point.X > MaxX) { MaxX = point.X; }
                            if (point.Y < MinY) { MinY = point.Y; }
                            if (point.Y > MaxY) { MaxY = point.Y; }
                            if (point.Z < MinZ) { MinZ = point.Z; }
                            if (point.Z > MaxZ) { MaxZ = point.Z; }
                        }
                        else
                        {
                            MinX = point.X;
                            MinY = point.Y;
                            MinZ = point.Z;

                            MaxX = point.X;
                            MaxY = point.Y;
                            MaxZ = point.Z;
                        }
                        i++;
                    }
                    double MidX = (MaxX + MinX) / 2;
                    double MidY = (MaxY + MinY) / 2;
                    double MidZ = (MaxZ + MinZ) / 2;
                    writeInput.Placement.Location = Vector3.Create(MidX, MidY, MidZ);
                }

            }
            #endregion

            #region IFC Version
            writeInput.SurfaceType = SurfaceType.TFS;
            if (jSettings.surfaceType == "GCS")
            { 
                writeInput.SurfaceType = SurfaceType.GCS; 
            }
            else if (jSettings.surfaceType == "SBSM")
            { 
                writeInput.SurfaceType = SurfaceType.SBSM; 
            }
            else if (jSettings.surfaceType == "TIN")
            {
                writeInput.SurfaceType = SurfaceType.TIN;
            }
            #endregion

            #region IFC Version Filetyp
            writeInput.FileType = FileType.Step;
            if (jSettings.outFileType == "XML")
            { writeInput.FileType = FileType.XML; }

            logger.Debug("Writing IFC with:");
            logger.Debug("IFC Version: " + jSettings.outIFCType);
            logger.Debug("Surfacetype: " + jSettings.surfaceType);
            logger.Debug("Filetype: " + jSettings.fileType);
            #endregion

            #region IFC2x3
            if (jSettings.outIFCType == "IFC2x3")
            {
                var model = WriteIfc2.CreateSite(jSettings.projectName,
                                                 jSettings.editorsFamilyName,
                                                 jSettings.editorsGivenName,
                                                 jSettings.editorsOrganisationName,
                                                 "Site with Terrain",
                                                 writeInput.Placement,
                                                 this.Mesh,
                                                 writeInput.SurfaceType,
                                                 breakDist);
                logger.Debug("IFC Site created");
                WriteIfc2.WriteFile(model, jSettings.destFileName, writeInput.FileType == FileType.XML);
                logger.Info("IFC file writen: " + jSettings.destFileName);
            }
            #endregion

            #region IFC4 - MESH
            else if (jSettings.outIFCType == "IFC4")
            {
                logger.Debug("Geographical Element: " + jSettings.geoElement);
                var model = jSettings.geoElement 
                    ? WriteIfc4.CreateSiteWithGeo(jSettings.projectName,
                                                 jSettings.editorsFamilyName,
                                                 jSettings.editorsGivenName,
                                                 jSettings.editorsOrganisationName,
                                                 "Site with Terrain",
                                                 writeInput.Placement,
                                                 this.Mesh,
                                                 writeInput.SurfaceType,
                                                 breakDist)
                    : WriteIfc4.CreateSite(jSettings.projectName,
                                                 jSettings.editorsFamilyName,
                                                 jSettings.editorsGivenName,
                                                 jSettings.editorsOrganisationName,
                                                 "Site with Terrain",
                                                 writeInput.Placement,
                                                 this.Mesh,
                                                 writeInput.SurfaceType,
                                                 breakDist);
                logger.Debug("IFC Site created");
                WriteIfc4.WriteFile(model, jSettings.destFileName, writeInput.FileType == FileType.XML);
                logger.Info("IFC file writen: " + jSettings.destFileName);
            }
            #endregion

            //DRAFT VERSION BELOW
            #region IFC4 - TIN
            if (jSettings.outIFCType == "IFC4TIN")
            {
                var model = jSettings.geoElement
                    ? WriteIfc4Tin.CreateSiteWithGeo(jSettings.projectName,
                                                    jSettings.editorsFamilyName,
                                                    jSettings.editorsGivenName,
                                                    jSettings.editorsOrganisationName,
                                                    //jSettings.minDist,
                                                    "Site with Terrain",
                                                    writeInput.Placement,
                                                    this.Tin,
                                                    this.Breaklines,
                                                    writeInput.SurfaceType,
                                                    breakDist)
                    : WriteIfc4Tin.CreateSite(jSettings.projectName,
                                                    jSettings.projectName,
                                                    jSettings.editorsFamilyName,
                                                    jSettings.editorsGivenName,
                                                    //jSettings.minDist,
                                                    "Site with Terrain",
                                                    writeInput.Placement,
                                                    this.Tin,
                                                    this.Breaklines,
                                                    writeInput.SurfaceType,
                                                    breakDist); ;
                WriteIfc4Tin.WriteFile(model, jSettings.destFileName, writeInput.FileType == FileType.XML);
            }
            #endregion

            #region IFC2x3 TIN
            if (jSettings.outIFCType == "IFC2x3TIN")
            {
                var model = WriteIfc2TIN.CreateSite(jSettings.projectName,
                                                 jSettings.editorsFamilyName,
                                                 jSettings.editorsGivenName,
                                                 jSettings.editorsOrganisationName,
                                                 "Site with Terrain",
                                                 writeInput.Placement,
                                                 this.Tin,
                                                 writeInput.SurfaceType,
                                                 breakDist);
                logger.Debug("IFC Site created");
                WriteIfc2TIN.WriteFile(model, jSettings.destFileName, writeInput.FileType == FileType.XML);
                logger.Info("IFC file writen: " + jSettings.destFileName);
            }

            #endregion



            #endregion
            logger.Info("----------------------------------------------");
        }
    }
}
