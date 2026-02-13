#ifndef PET_PROJECT_CONFIG_H_
#define PET_PROJECT_CONFIG_H_

#include <string>
#include <vector>

namespace evo {

struct Config {
  int population_size = 100;
  float mutation_rate = 0.02f;
  int dna_length = 50;
  int imagenet_class = 281;
  float random_ratio = 0.1f;
  float parent1_top_ratio = 0.1f;
  float parent2_top_ratio = 0.9f;
  int image_width = 224;
  int image_height = 224;
  std::vector<std::string> model_paths = {"mobilenetv2-12.onnx"};
  float max_triangle_radius = 0.3f;
  std::string output_dir = "output";
  bool benchmark = false;

  static Config load(const std::string& path);
};

}  // namespace evo

#endif  // PET_PROJECT_CONFIG_H_
