#ifndef PET_PROJECT_EVOLUTION_H_
#define PET_PROJECT_EVOLUTION_H_

#include <random>
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

 private:
  std::pair<Individual, Individual> crossover(const Individual& p1,
                                              const Individual& p2);
  void mutate(Individual& ind);

  Config config_;
  Renderer& renderer_;
  Fitness& fitness_;
  std::vector<Individual> population_;
  int generation_ = 0;
  std::mt19937 rng_;
};

}  // namespace evo

#endif  // PET_PROJECT_EVOLUTION_H_
