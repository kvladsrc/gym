#ifndef PET_PROJECT_EVOLUTION_H_
#define PET_PROJECT_EVOLUTION_H_

#include <deque>
#include <random>
#include <utility>
#include <vector>

#include "pet_project/config.hpp"
#include "pet_project/fitness.hpp"
#include "pet_project/renderer.hpp"
#include "pet_project/types.hpp"

namespace evo {

class EvolutionEngine {
 public:
  EvolutionEngine(const Config& config, Renderer& renderer, Fitness& fitness);

  void init_population();
  void step();

  const Individual& best() const;
  int generation() const;
  int current_grid_size() const;
  bool finished() const;

 private:
  std::pair<Individual, Individual> crossover(const Individual& p1,
                                              const Individual& p2);
  void mutate(Individual& ind);
  void refine_grid();
  bool should_refine_grid() const;

  Config config_;
  Renderer& renderer_;
  Fitness& fitness_;
  std::vector<Individual> population_;
  int generation_ = 0;
  int current_grid_size_;
  std::mt19937 rng_;
  
  // Fitness tracking for plateau detection (monotonic deque)
  // Stores pairs of (generation_at_current_grid, fitness) in non-decreasing fitness order
  std::deque<std::pair<int, float>> fitness_history_;
  int generations_at_current_grid_ = 0;
};

}  // namespace evo

#endif  // PET_PROJECT_EVOLUTION_H_
