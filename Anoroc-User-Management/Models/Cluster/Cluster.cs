﻿using Anoroc_User_Management.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Anoroc_User_Management.Interfaces;

namespace Anoroc_User_Management.Services
{
    /// <summary>
    /// Class helping the cluster service
    /// </summary>
    /// 
    
    public class Cluster
    {
        [Key]
        public long Cluster_Id { get; set; }
        [ForeignKey("ClusterReferenceID")]
        public ICollection<Location> Coordinates { get; set; } = new List<Location>();
        [ForeignKey("Location_ID")]
        public long Center_LocationLocation_ID { get; set; }
        public Location Center_Location { get; set; } = new Location();
        public int Carrier_Data_Points;
        public DateTime Cluster_Created { get; set; }
        public IDatabaseEngine DatabaseEngine;
   

        public double Cluster_Radius { get; set; }
        public Cluster()
        {
            Coordinates = new List<Location>();
            Cluster_Created = DateTime.Now;
            // TODO:
        }
            // Create a function that scans through the list of clusters and removes the ones that have been there the longest
        

        public Cluster(Location loc, long cluster_id)
        {

            Coordinates = new List<Location>();

            Coordinates.Add(loc);

            Cluster_Created = DateTime.Now;

            Cluster_Id = cluster_id;

            

            if (loc.Carrier_Data_Point)
                Carrier_Data_Points++;

           
        }

        public Cluster(Location loc, long cluster_id, IDatabaseEngine database)
        {

            Coordinates = new List<Location>();

            Coordinates.Add(loc);

            Cluster_Created = DateTime.Now;

            Cluster_Id = cluster_id;

            DatabaseEngine = database;

            if (loc.Carrier_Data_Point)
                Carrier_Data_Points++;

          
        }
        public Cluster(ICollection<Location> coords, long cluster_id)
        {
            Coordinates = coords;
            Cluster_Id = cluster_id;
            foreach(Location loc in coords)
                if (loc.Carrier_Data_Point)
                    Carrier_Data_Points++;
        }

        public Cluster(Cluster cluster)
        {
            Cluster_Id = cluster.Cluster_Id;

            foreach (Location location in cluster.Coordinates)
            {
                Coordinates.Add(location);
            }
            Carrier_Data_Points = cluster.Carrier_Data_Points;
            DatabaseEngine = cluster.DatabaseEngine;
            Cluster_Radius = cluster.Cluster_Radius;
        }

        public Cluster(ICollection<Location> coordinates, Location center_Location, int carrier_Data_Points, DateTime cluster_Created, double cluster_Radius)
        {
            Coordinates = coordinates;
            Center_Location = center_Location;
            Carrier_Data_Points = carrier_Data_Points;
            Cluster_Created = cluster_Created;
            Cluster_Radius = cluster_Radius;
        }

        public Cluster(OldCluster cluster)
        {
            Cluster_Id = cluster.Old_Cluster_Id;
            foreach (OldLocation location in cluster.Coordinates)
            {
                Coordinates.Add(new Location(location));
            }
            Center_Location = new Location(cluster.Center_Location);
            Carrier_Data_Points = cluster.Carrier_Data_Points;
            DatabaseEngine = cluster.DatabaseEngine;
            Cluster_Radius = cluster.Cluster_Radius;
        }

        public void Structurize()
        {
            Calculate_Center();
            Calculate_Radius();
        }


        /// <summary>
        /// Function that checks if a location provided belongs to this cluster based on the distance between any ONE location already in the cluster.
        /// </summary>
        /// <param name="location"> The location being tested to see if it belongs in the cluster. </param>
        /// <returns> True if the location belongs in the cluster, False otherwise. </returns>
        public bool Check_If_Belong(Location location)
        {
            bool belongs = false;
            var dist = HaversineDistance(location, Center_Location);
            if (dist <= 200)
            { 
                belongs = true;
            }
            return belongs;
        }

        /// <summary>
        /// Function that checks if a location provided belongs to this cluster based on the distance between any ONE location already in the cluster.
        /// </summary>
        /// <param name="location"></param>
        /// <returns>true if the cluster contains the location. false if it doesn't</returns>
        public bool Contains(Location location)
        {
            var dist = HaversineDistance(location, Center_Location);

            var contains = dist <= 200;
            return contains;
        }

        /// <summary>
        ///  Adds a new location point into the cluster
        /// </summary>
        /// <param name="newCoord"> The new location point to be added into the cluster </param>
        public void AddLocation(Location newCoord)
        {
            //newCoord.ClusterReferenceID = Cluster_Id;
            Coordinates.Add(newCoord);
            if (newCoord.Carrier_Data_Point)
                Carrier_Data_Points++;
        }


        /// <summary>
        /// Gets the percentage of the cluster that is made up of location points from carriers
        /// </summary>
        /// <returns> The percentage of carriers in the cluster </returns>
        public double Percentage_Carrier()
        {
            double percentage;
            if (Carrier_Data_Points > 0)
            {
                percentage = Carrier_Data_Points / Coordinates.Count;
                return Math.Round(percentage, 2, MidpointRounding.AwayFromZero);
            }
            else
                return 0;
        }

        /// <summary>
        /// Calculate center point of a cluster, code adapated from: https://gist.github.com/tlhunter - average-geolocation.js
        /// </summary>
        public void Calculate_Center()
        {
            Center_Location = null;
            Cluster_Created = Coordinates.ElementAt(0).Created;

            if (Coordinates.Count == 1)
            {
                Center_Location = Coordinates.ElementAt(0);
            }

            var x = 0.0;
            var y = 0.0;
            var z = 0.0;

            foreach (var coord in Coordinates)
            {
                var latitude = coord.Latitude * Math.PI / 180;
                var longitude = coord.Longitude * Math.PI / 180;

                x += Math.Cos(latitude) * Math.Cos(longitude);
                y += Math.Cos(latitude) * Math.Sin(longitude);
                z += Math.Sin(latitude);
            }
            var total = Coordinates.Count;

            x /= total;
            y /= total;
            z /= total;

            var centralLongitude = Math.Atan2(y, x);
            var centralSquareRoot = Math.Sqrt(x * x + y * y);
            var centralLatitude = Math.Atan2(z, centralSquareRoot);

            Center_Location = new Location((centralLatitude * 180 / Math.PI), (centralLongitude * 180 / Math.PI), Cluster_Created, Coordinates.ElementAt(0).Region);
            Center_Location.Carrier_Data_Point = true;
            if(DatabaseEngine != null)
                DatabaseEngine.Insert_Location(Center_Location);
        }

        /// <summary>
        /// Calculate the radius of the  cluster for drawing a circle on the map. The radius is calculated as the max(distance from a point to the center point)
        /// </summary>
        public void Calculate_Radius()
        {
            double radius = 0;
            int cluster_size = Coordinates.Count;
            double temp_distance;
            for (int i = 0; i < cluster_size - 1; i++)
            {
                temp_distance = HaversineDistance(Coordinates.ElementAt(i), Center_Location);
                if (temp_distance > radius)
                    radius = temp_distance;
            }
            Cluster_Radius = radius;
        }

        public static double HaversineDistance(Location firstLocation, Location secondLocation)
        {
      
            double earthRadius = 6371.0; // kilometers (or 3958.75 miles)

            var dLat = (firstLocation.Latitude - secondLocation.Latitude) * Math.PI/180;   //Math.ToRadians(lat2 - lat1);

            double dLng = (firstLocation.Longitude - secondLocation.Longitude) * Math.PI/180;

            double sindLat = Math.Sin(dLat / 2);

            double sindLng = Math.Sin(dLng / 2);

            double a = Math.Pow(sindLat, 2) + Math.Pow(sindLng, 2)
                        * Math.Cos(firstLocation.Latitude * Math.PI/180) * Math.Cos(secondLocation.Latitude * Math.PI/180);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double dist = earthRadius * c;

            return dist * 1000; // dist is in KM so must convert to meter
        }
    }
}
