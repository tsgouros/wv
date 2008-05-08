using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libCoin3D;


namespace libWrist
{
    public class DistanceMaps
    {
        private TransformMatrix[][] _transformMatrices;
        private ColoredBone[] _colorBones;
        private Wrist _wrist;

        private CTmri[] _distanceFields;
        private int[][][] _calculatedColorMaps;  //stores the packed colors in 32bit INT values.
        private double[][][] _calculatedDistances;

        public DistanceMaps(Wrist wrist, TransformMatrix[][] transformMatrices, ColoredBone[] colorBones)
        {
            _wrist = wrist;
            _transformMatrices = transformMatrices;
            _colorBones = colorBones;
        }

        private void readInDistanceFieldsIfNotLoaded()
        {
            if (_distanceFields != null)
                return;

            _distanceFields = new CTmri[Wrist.NumBones];

            for (int i = 0; i < Wrist.NumBones; i++)
            {
                string basefolder = Path.Combine(Path.Combine(_wrist.subjectPath, _wrist.neutralSeries), "DistanceFields");
                string folder = String.Format("{0}{1}_mri", Wrist.ShortBoneNames[i], _wrist.neutralSeries.Substring(1, 3));
                if (Directory.Exists(Path.Combine(basefolder, folder)))
                {
                    _distanceFields[i] = new CTmri(Path.Combine(basefolder, folder));
                    _distanceFields[i].loadImageData();
                }
                else
                    _distanceFields[i] = null;
            }
        }

        private bool hasDistanceMapsForBonePosition(int boneIndex, int positionIndex)
        {
            if (_calculatedDistances == null)
                return false;

            if (_calculatedDistances[boneIndex] == null)
                return false;

            return (_calculatedDistances[boneIndex][positionIndex] != null);
        }

        private bool hasDistanceColorMapsForPosition(int positionIndex)
        {
            if (_calculatedColorMaps == null)
                return false;

            //only check the radius, it should be a good enough check....
            if (_calculatedColorMaps[0] == null)
                return false;

            return (_calculatedColorMaps[0][positionIndex] != null);
        }


        public void loadDistanceColorMapsForPositionIfCalculatedOrClear(int positionIndex)
        {
            if (hasDistanceColorMapsForPosition(positionIndex))
                loadDistanceColorMapsForPosition(positionIndex);
            else
                clearDistanceColorMapsForAllBones();
        }

        public void readInAllDistanceColorMaps()
        {
            //setup save space if it doesn't exist
            if (_calculatedColorMaps == null)
                _calculatedColorMaps = new int[Wrist.NumBones][][];

            readInDistanceFieldsIfNotLoaded();

            //try and create color scheme....
            for (int i = 0; i < Wrist.NumBones; i++)
            {
                //setup space if it doesn't exist
                if (_calculatedColorMaps[i] == null)
                    _calculatedColorMaps[i] = new int[_transformMatrices.Length + 1][]; //add one extra for neutral :)

                //now read the color map for each position index
                for (int j = 0; j < _transformMatrices.Length + 1; j++)
                {
                    //read in the colors if not yet loaded
                    if (_calculatedColorMaps[i][j] == null)
                        _calculatedColorMaps[i][j] = createColormap(i, j);
                }
            }
        }

        public void loadDistanceColorMapsForPosition(int positionIndex)
        {
            //setup save space if it doesn't exist
            if (_calculatedColorMaps == null)
                _calculatedColorMaps = new int[Wrist.NumBones][][];

            readInDistanceFieldsIfNotLoaded();

            //try and create color scheme....
            for (int i = 0; i < Wrist.NumBones; i++)
            {
                //setup space if it doesn't exist
                if (_calculatedColorMaps[i] == null)
                    _calculatedColorMaps[i] = new int[_transformMatrices.Length + 1][]; //add one extra for neutral :)

                //read in the colors if not yet loaded
                if (_calculatedColorMaps[i][positionIndex] == null)
                    _calculatedColorMaps[i][positionIndex] = createColormap(i, positionIndex);

                //now set that color
                _colorBones[i].setColorMap(_calculatedColorMaps[i][positionIndex]);
            }
        }

        public void clearDistanceColorMapsForAllBones()
        {
            for (int i = 0; i < Wrist.NumBones; i++)
            {
                if (_colorBones[i] != null)
                    _colorBones[i].clearColorMap();
            }
        }

        private TransformMatrix[] calculateRelativeMotionForDistanceMaps(int boneIndex, int positionIndex, int[] boneInteraction)
        {
            TransformMatrix[] tmRelMotions = new TransformMatrix[Wrist.NumBones]; //for each position
            if (positionIndex == 0) //no transforms needed for the neutral position, we are all set :)
                return tmRelMotions;

            /* Check if we are missing kinematics for the bone, if so, then we can not
             * calculate distance maps (we don't know where the bone is, so we just return all null)
             */
            if (_transformMatrices[positionIndex - 1][boneIndex] == null ||
                _transformMatrices[positionIndex - 1][boneIndex].isIdentity())
                return tmRelMotions;

            TransformMatrix tmBone = _transformMatrices[positionIndex - 1][boneIndex];
            foreach (int testBoneIndex in boneInteraction)
            {
                //Again, check if there is no kinematics for the test bone, again, if none, just move on
                if (_transformMatrices[positionIndex - 1][testBoneIndex] == null ||
                _transformMatrices[positionIndex - 1][testBoneIndex].isIdentity())
                    continue;

                TransformMatrix tmFixedBone = _transformMatrices[positionIndex - 1][testBoneIndex];
                //so fix the current bone, and move our test bone to that position....yes?
                tmRelMotions[testBoneIndex] = tmFixedBone.Inverse() * tmBone;
            }
            return tmRelMotions;

        }


        private double[] getOrCalculateDistanceMap(int boneIndex, int positionIndex)
        {
            //setup save space if it doesn't exist
            if (_calculatedDistances == null)
                _calculatedDistances = new double[Wrist.NumBones][][];

            readInDistanceFieldsIfNotLoaded();

            if (_calculatedDistances[boneIndex]==null)
                _calculatedDistances[boneIndex] = new double[_transformMatrices.Length+1][]; //add one extra for neutral
            
            if (_calculatedDistances[boneIndex][positionIndex]==null)
                _calculatedDistances[boneIndex][positionIndex] = createDistanceMap(_distanceFields,boneIndex,positionIndex);

            return _calculatedDistances[boneIndex][positionIndex];
        }

        private double[] createDistanceMap(CTmri[] mri, int boneIndex, int positionIndex)
        {
            float[,] pts = _colorBones[boneIndex].getVertices();
            int numVertices = pts.GetLength(0);

            double[] distances = new double[numVertices];
            int[] interaction = Wrist.BoneInteractionIndex[boneIndex]; //load  bone interactions

            TransformMatrix[] tmRelMotions = calculateRelativeMotionForDistanceMaps(boneIndex, positionIndex, interaction);

            //for each vertex           
            for (int i = 0; i < numVertices; i++)
            {
                distances[i] = Double.MaxValue; //set this vertex to the default
                //for (int j = 0; j < Wrist.NumBones; j++)
                foreach (int j in interaction) //only use the bones that we have specified interact
                {
                    if (j == boneIndex) continue;
                    if (mri[j] == null) continue; //skip missing scans

                    double x = pts[i, 0];
                    double y = pts[i, 1];
                    double z = pts[i, 2];

                    //check if we need to move for non neutral position
                    if (positionIndex != 0)
                    {
                        //skip missing kinematic info
                        if (tmRelMotions[j] == null)
                            continue;

                        //lets move the bone getting colored, into the space of the other bone...
                        double[] p0 = new double[] { x, y, z };
                        double[] p1 = tmRelMotions[j] * p0;
                        x = p1[0];
                        y = p1[1];
                        z = p1[2];
                    }

                    double dX = (x - mri[j].CoordinateOffset[0]) / mri[j].voxelSizeX;
                    double dY = (y - mri[j].CoordinateOffset[1]) / mri[j].voxelSizeY;
                    double dZ = (z - mri[j].CoordinateOffset[2]) / mri[j].voxelSizeZ;

                    const double xBound = 96.9; //get the boundaries of the distance cube
                    const double yBound = 96.9; //
                    const double zBound = 96.9; //

                    ////////////////////////////////////////////////////////
                    //is surface point picked inside of the cube?

                    if (dX >= 3.1 && dX <= xBound && dY >= 3.1
                        && dY <= yBound && dZ >= 3.1 && dZ <= zBound)
                    {
                        double localDist = mri[j].sample_s_InterpCubit(dX, dY, dZ);
                        if (localDist < distances[i]) //check if this is smaller, if so save it
                            distances[i] = localDist;
                    }
                }
            }
            return distances;
        }


        private int[] createColormap(int boneIndex, int positionIndex)
        {
            double[] distances = getOrCalculateDistanceMap(boneIndex, positionIndex);
            int numVertices = distances.Length;
            int[] colors = new int[numVertices];

            for (int i = 0; i < numVertices; i++)
            {
                UInt32 packedColor;

                //first check if there is collission
                if (distances[i] < 0)
                {
                    //color collision in blue
                    packedColor = 0X0000FFFF;
                }
                // a parameter could be used instead of plain 3
                else if (distances[i] > 3)  //check if we are too far away
                {
                    //make us white
                    packedColor = 0xFFFFFFFF;
                }
                else
                {
                    /* convert to packed RGB color....how?
                     * packed color for Coin3D/inventor is 0xRRGGBBAA
                     * So take our GB values (should be from 0-255 or 8 bits), and move from
                     * Lest significant position (0x000000XX) to the G and B position, then
                     * combine with a bitwise OR. (0x00XX0000 | 0x0000XX00), which gives us
                     * the calculated value in both the G & B slots, and 0x00 in R & A.
                     * So we then ahve 0x00GGBB00, we can then bitwise OR with 0xFF0000FF, 
                     * since we want both R and Alpha to be at 255. Then we are set :)
                     */
                    uint GB = (uint)(distances[i] * 255.0 / 3.0);
                    packedColor = (GB << 16) | (GB << 8) | 0xFF0000FF;
                }
                colors[i] = (int)packedColor;
            }
            return colors;
        }

        public Contour createContourShit()
        {
            double[] dist = _calculatedDistances[0][0];
            float[,] points = _colorBones[0].getVertices();
            int[,] conn = _colorBones[0].getFaceSetIndices();

            Contour cont1 = new Contour();

            double contourDistance = 1.0;

            int numTrian = conn.GetLength(0);
            for (int i=0; i<numTrian; i++)
            {
                //check if all the points are out, if so, skip it
                if (dist[conn[i, 0]] > contourDistance &&
                    dist[conn[i, 1]] > contourDistance &&
                    dist[conn[i, 2]] > contourDistance)
                    continue;

                double[] triDist = { dist[conn[i, 0]], dist[conn[i, 1]], dist[conn[i, 2]] };
                float[][] triPts = { 
                    new float[] {points[conn[i, 0], 0], points[conn[i, 0], 1], points[conn[i, 0], 2]},
                    new float[] {points[conn[i, 1], 0], points[conn[i, 1], 1], points[conn[i, 1], 2]},
                    new float[] {points[conn[i, 2], 0], points[conn[i, 2], 1], points[conn[i, 2], 2]}
                };

                contourSingleTriangle(triDist, triPts, cont1);
            }
            return cont1;
        }

        private void contourSingleTriangle(double[] dist, float[][] vertices, Contour contour)
        {
            //for each contour....

            double cDist = 1.0;

            int[] inside = { 0, 0, 0 };
            int[] outside = { 0, 0, 0 };
            int numInside = 0;
            int numOutside = 0;

            //check if each point is inside or ouside
            for (int i = 0; i < 3; i++)
            {
                if (dist[i] < cDist) //is inside
                    inside[numInside++] = i; //add to array and incriment
                else
                    outside[numOutside++] = i;
            }

            //skip triangles totally outside
            if (numOutside == 3)
                return;

            //check triangles totally inside
            if (numInside == 3)
            {
                //TODO: Add distance to aread
                return;
            }

            float[] newPt1, newPt2;
            //so I now have one or two vertices inside, and one or two ouside.....what to do
            if (numInside == 1)
            {
                newPt1 = createGradientPoint(dist[inside[0]], vertices[inside[0]], dist[outside[0]], vertices[outside[0]], cDist);
                newPt2 = createGradientPoint(dist[inside[0]], vertices[inside[0]], dist[outside[1]], vertices[outside[1]], cDist);
                //TODO: calculate area
            }
            else //I have 2 inside....yay
            {
                newPt1 = createGradientPoint(dist[outside[0]], vertices[outside[0]], dist[inside[0]], vertices[inside[0]], cDist);
                newPt2 = createGradientPoint(dist[outside[0]], vertices[outside[0]], dist[inside[1]], vertices[inside[1]], cDist);
                //TODO: calculate area
            }
            contour.addLineSegment(newPt1[0], newPt1[1], newPt1[2], newPt2[0], newPt2[1], newPt2[2]);
        }

        private float[] createGradientPoint(double d0, float[] v0, double d1, float[] v1, double cDist)
        {
            float[] midpoint = new float[3];

            double ratio = (d0 - cDist) / (d0 - d1); //fraction of contribution for v1 (its how far away we are)
            System.Diagnostics.Debug.Assert(ratio > 0); //quick check :)
            midpoint[0] = (float)((1 - ratio) * v0[0] + ratio * v1[0]);
            midpoint[1] = (float)((1 - ratio) * v0[1] + ratio * v1[1]);
            midpoint[2] = (float)((1 - ratio) * v0[2] + ratio * v1[2]);
            return midpoint;
        }
    }
}
