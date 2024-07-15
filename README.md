# Starburst Planner

This repository contains a C# program designed to plan a starburst pattern of links between portals in the game Ingress. It reads map data from a JSON file, processes the data to filter portals and links based on distance, and then plans the starburst by selecting portals that are not blocked by existing links.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Algorithm Description](#algorithm-description)
- [License](#license)

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/yourusername/starburst-planner.git
    ```

2. Navigate to the project directory:
    ```sh
    cd starburst-planner
    ```

3. Restore the required NuGet packages:
    ```sh
    dotnet restore
    ```

4. Build the project:
    ```sh
    dotnet build
    ```

## Usage

To run the program, use the following command:
```sh
dotnet run -- /inputFilePath=<input_file_path> /outputFilePath=<output_file_path> /targetPortalGuid=<target_portal_guid> /targetLinksCount=<target_links_count> /maxDistanceKm=<max_distance_km>
```

### Command-Line Arguments

- `/inputFilePath`: Path to the input JSON file containing map data.
- `/outputFilePath`: Path to the output JSON file where results will be saved.
- `/targetPortalGuid`: GUID of the target portal for the starburst.
- `/targetLinksCount`: Number of links to be created in the starburst.
- `/maxDistanceKm`: Maximum distance in kilometers from the target portal to consider other portals.

### Example
```sh
dotnet run -- /inputFilePath="C:\\path\\to\\input.json" /outputFilePath="C:\\path\\to\\output.json" /targetPortalGuid="888af823724633ef9c6f7d3564976640.16" /targetLinksCount=1400 /maxDistanceKm=6.0
```

## Algorithm Description

1. **Parse Command-Line Arguments**: The program starts by parsing command-line arguments to get the input file path, output file path, target portal GUID, target number of links, and maximum distance.

2. **Read and Parse Map Data**: The program reads the map data from the input JSON file and deserializes it into a `MapData` object.

3. **Filter Portals and Links**: The program filters portals and links based on the maximum distance from the target portal.

4. **Calculate Blocking Information**: The program calculates which portals are blocking other portals by intersecting links.

5. **Plan Starburst**: The program plans the starburst by selecting portals that are not blocked by existing links. It iteratively neutralizes blocking portals until the desired number of linkable portals is reached.

6. **Format Output**: The program formats the selected portals and links into a JSON output that can be used for visualization.

7. **Write Output**: The program writes the formatted output to the specified output file.

### Distance Calculation
The distance between two geographical points is calculated using the Haversine formula, which accounts for the Earth's curvature.

### Link Intersection
The program checks if a link between two portals intersects with any existing links by using geometric line intersection formulas.

### Blocking Information
Portals that block other portals are identified, and their blocking information is updated iteratively until the desired number of linkable portals is obtained.

## License

This project is licensed under the MIT License

---

Feel free to contribute to this project by opening issues or submitting pull requests. Happy linking!
