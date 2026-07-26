# MCDomain_synapses.sql — Plain-English Explanation

This document describes the analysis performed by `MCDomain_synapses.sql`: finding synapses near a chosen glial cell in a connectome database, measuring 3D proximity, and exporting geometry for inspection or plotting.

---

## Plain-English Summary

This analysis finds **synapses near a chosen glial cell**, measures how close they are in three dimensions, and exports enough geometry to inspect or plot the results.

### What question it answers

For one glial structure (here, structure ID **8887**), it asks:

> Among synapses of selected types, which ones lie within **500 nm** of the glial cell, and what is the **closest distance** from the glial cell to each synapse structure?

It is meant for **spatial analysis of synapse–glia proximity**, not for changing annotations.

### Step-by-step logic

1. **Find candidate synapse locations near the glial cell**  
   For every location belonging to the glial structure, the analysis looks for synapse locations that are:
   - on nearby sections (within a depth window corresponding to 500 nm), and
   - within 500 nm in the lateral (x/y) plane, measured as the spatial distance between annotated volume geometries.

   Each candidate records the glial location, synapse location, parent synapse structure, and separate lateral and depth distance components.

2. **Export candidate details (first result set)**  
   Results include the glial and synapse shapes, location and structure identifiers, and distance measurements. This output is suitable for CSV export and visual checking.

3. **Compute one distance per synapse structure**  
   A synapse may have multiple annotated locations. The analysis groups by synapse structure and keeps the **minimum 3D distance**:
   \[
   \text{distance} = \sqrt{(\text{lateral distance})^2 + (\text{depth distance})^2}
   \]

4. **Count distinct nearby synapses**  
   The analysis reports how many synapse structures have at least one location within range.

5. **Keep the closest glial–synapse location pair for each synapse structure**  
   For each synapse structure at its minimum distance, the analysis records which glial location was closest.

6. **Build merged shapes for visualization (second main result set)**  
   For each qualifying synapse structure and for the glial cell, all constituent location annotations are merged into a single geometry. These merged shapes are exported with structure identifiers for mapping or downstream analysis.

### One-sentence version

Nearby synapses were identified by screening annotated glial and synapse locations, retaining synapse structures within 500 nm (3D) of a target glial structure, assigning each synapse structure its minimum glial–synapse distance, and exporting merged annotation geometries for visualization.

---

## Data Structures

### Paper-ready version

Annotations were stored as location-level geometric objects grouped by parent structure identifiers. Each location carried a volume geometry and a section (depth) coordinate. Synapse candidates were identified by structure type and screened for proximity to a designated glial structure. Lateral separation was computed as the spatial distance between annotated volume geometries, and depth separation was computed from section coordinates after conversion to physical units using volume-specific lateral and axial scale factors. For each synapse structure, the minimum three-dimensional distance to the glial cell was retained.

### Concepts at a glance

| Concept | Role in the analysis |
|---|---|
| **Location** | Individual annotated mark or shape in the volume; has volume geometry and section depth |
| **Structure** | Parent object (e.g., glial cell, synapse); groups one or more locations |
| **Structure type** | Classifies structures (e.g., which IDs count as synapses); here types **73, 34, and 28** |
| **Volume geometry** | The annotated shape used to compute lateral (in-plane) distance |
| **Section depth (Z)** | Used to restrict the search to nearby sections and compute depth separation |
| **Scale factors** | Convert database coordinates to nanometers for x/y and z |
| **Merged geometry** | All locations belonging to one structure combined into one shape for export |

In this run, the glial cell of interest is structure **8887**, and synapses are defined by structure type IDs **73, 34, and 28**. The proximity threshold is **500 nm**.

---

## Methods Paragraph (Journal Style)

Synapse–glia proximity was quantified using a spatial query against the annotated connectome database. Annotations were stored as location-level geometric objects grouped by parent structure identifiers. For a designated glial structure, all annotated synapse locations belonging to predefined synapse structure types were screened for proximity within a 500 nm search radius. In-plane separation was computed from the spatial distance between annotated volume geometries, and depth separation was computed from section coordinates after conversion to physical units using volume-specific lateral and axial scale factors. For each synapse structure, the minimum three-dimensional distance to the glial cell was retained, and the number of synapse structures meeting the proximity criterion was recorded. To support visualization and downstream analysis, merged annotation geometries were generated for each qualifying synapse structure and for the glial cell by aggregating all constituent location annotations. Results were exported as tabular outputs containing structure identifiers, distance measurements, and geometry representations suitable for mapping.

---

## Suggested Citation Wording (Optional)

> Spatial proximity between glial cells and synapses was assessed using a custom query applied to the connectome annotation database, with a 500 nm proximity threshold and minimum-distance assignment per synapse structure.
