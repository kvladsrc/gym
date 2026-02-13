#include "pet_project/config.hpp"

#include <fstream>
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace evo {

Config Config::load(const std::string& path) {
  std::ifstream f(path);

  if (!f.is_open()) {
    throw std::runtime_error("Cannot open config file: " + path);
  }

  auto j = nlohmann::json::parse(f);
  Config c;

  c.population_size = j.value("population_size", c.population_size);
  c.mutation_rate = j.value("mutation_rate", c.mutation_rate);
  c.dna_length = j.value("dna_length", c.dna_length);
  c.imagenet_class = j.value("imagenet_class", c.imagenet_class);
  c.random_ratio = j.value("random_ratio", c.random_ratio);
  c.parent1_top_ratio = j.value("parent1_top_ratio", c.parent1_top_ratio);
  c.parent2_top_ratio = j.value("parent2_top_ratio", c.parent2_top_ratio);
  c.image_width = j.value("image_width", c.image_width);
  c.image_height = j.value("image_height", c.image_height);
  c.max_triangle_radius = j.value("max_triangle_radius", c.max_triangle_radius);
  c.model_paths = j["model_paths"].get<std::vector<std::string>>();
  c.output_dir = j.value("output_dir", c.output_dir);
  c.benchmark = j.value("benchmark", c.benchmark);

  return c;
}

}  // namespace evo
