#ifndef PET_PROJECT_CONFIG_H_
#define PET_PROJECT_CONFIG_H_

#include <string>
#include <vector>

namespace evo {

struct Config {
  int population_size = 100;
  float mutation_rate = 0.02f;
  int imagenet_class = 281;
  float random_ratio = 0.1f;
  float parent1_top_ratio = 0.1f;
  float parent2_top_ratio = 0.9f;
  int image_width = 224;
  int image_height = 224;
  std::vector<std::string> model_paths = {"mobilenetv2-12.onnx"};

  std::string output_dir = "output";
  int print_interval = 10;  // Print stats every N generations
  int save_interval = 100;  // Save image every N generations
  bool use_gpu = false;
  float blur_strength = 1.0f;  // 0.0 = no blur, 1.0 = full 3x3 box blur

  // Adaptive grid refinement
  int initial_grid_size = 4;  // Start with 4x4 grid
  int max_grid_size = 256;    // Maximum grid size (will be scaled to 224x224)
  int stabilization_generations = 50;  // Check for fitness plateau after N gens
  float fitness_improvement_threshold = 0.001f;  // Min improvement to continue
  bool divide_elite =
      true;  // Fill population with copies of best after subdivide
  bool divide_interpolate =
      false;  // Use bilinear interpolation when subdividing DNA

  static Config load(const std::string& path);
};

}  // namespace evo

#endif  // PET_PROJECT_CONFIG_H_
