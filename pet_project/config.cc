#include "pet_project/config.hpp"

#include <fstream>
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <string>

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
  c.imagenet_class = j.value("imagenet_class", c.imagenet_class);
  c.random_ratio = j.value("random_ratio", c.random_ratio);
  c.parent1_top_ratio = j.value("parent1_top_ratio", c.parent1_top_ratio);
  c.parent2_top_ratio = j.value("parent2_top_ratio", c.parent2_top_ratio);
  c.image_width = j.value("image_width", c.image_width);
  c.image_height = j.value("image_height", c.image_height);
  c.model_paths = j["model_paths"].get<std::vector<std::string>>();
  c.output_dir = j.value("output_dir", c.output_dir);
  c.print_interval = j.value("print_interval", c.print_interval);
  c.save_interval = j.value("save_interval", c.save_interval);
  c.use_gpu = j.value("use_gpu", c.use_gpu);
  c.blur_strength = j.value("blur_strength", c.blur_strength);
  c.initial_grid_size = j.value("initial_grid_size", c.initial_grid_size);
  c.max_grid_size = j.value("max_grid_size", c.max_grid_size);
  c.stabilization_generations =
      j.value("stabilization_generations", c.stabilization_generations);
  c.fitness_improvement_threshold =
      j.value("fitness_improvement_threshold", c.fitness_improvement_threshold);
  c.divide_elite = j.value("divide_elite", c.divide_elite);
  c.divide_interpolate = j.value("divide_interpolate", c.divide_interpolate);

  return c;
}

}  // namespace evo
