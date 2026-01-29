# tmdl_utility

A utility for converting 3D model formats, especially for Nintendo Switch BFRES files.

## Features

- Import from BFRES (.bfres, .bfres.zs, .szs) and standard formats (via Assimp)
- Export to GLTF, FBX, DAE, and other formats
- Support for meshes, materials, skeletons
- Animation data preservation (limited export support - see below)
- Texture extraction and conversion

## Export Formats

The tool uses **Aspose.3D** for exporting to various formats:
- **GLTF/GLB**: Widely supported format for modern 3D applications
- **FBX**: Industry standard for 3D content creation tools (best for animations)
- **DAE (Collada)**: Open standard format
- And many more supported by Aspose.3D

## Animation Support

- **Import**: Full support for importing animations from BFRES files
- **Export**: Basic animation data logging.
  - Animation metadata and channel information is preserved and logged during export
  - For production use with animations, FBX format is recommended as it has the best animation support in Aspose.3D
  - Full keyframe animation export is complex and format-dependent, requiring additional implementation

## Recent Changes

### Memory Corruption Fix

Previous versions used AssimpNet for exporting with animations, which caused "Attempted to read or write protected memory" errors due to P/Invoke marshalling issues in the AssimpNet library. 

**Solution**: We now use Aspose.3D exclusively for all exports. Aspose.3D is a reliable, commercial-grade library that properly handles format conversions without memory corruption issues.

## Usage

```bash
tmdl_utility Single <input_file> <output_directory>
```

Example:
```bash
tmdl_utility Single "test/pigeon.zs" "output/"
```

The output format is determined by the EXP_MDL define and the file extension in the code.
