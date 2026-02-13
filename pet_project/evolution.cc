#include "pet_project/evolution.hpp"

#include <algorithm>
#include <cmath>

#include "pet_project/types.hpp"

namespace evo {

EvolutionEngine::EvolutionEngine(const Config& config, Renderer& renderer,
                                 Fitness& fitness)
    : config_(config),
      renderer_(renderer),
      fitness_(fitness),
      rng_(std::random_device{}()) {}

void EvolutionEngine::init_population() {
  population_.resize(config_.population_size);
  for (auto& ind : population_) {
    ind.dna = random_dna(config_.dna_length, rng_, config_.max_triangle_radius);
    ind.fitness = 0.0f;
  }

  // Evaluate fitness
  fitness_.evaluate_population(population_, renderer_);

  // Sort by fitness descending
  std::sort(population_.begin(), population_.end(),
            [](const Individual& a, const Individual& b) {
              return a.fitness > b.fitness;
            });

  generation_ = 0;
}

void EvolutionEngine::step() {
  generation_++;

  int pop_size = config_.population_size;
  int num_random =
      static_cast<int>(std::round(config_.random_ratio * pop_size));
  int num_children = pop_size - num_random;
  if (num_children % 2 != 0) {
    num_children--;
    num_random++;
  }

  int parent1_pool =
      std::max(1, static_cast<int>(config_.parent1_top_ratio * pop_size));
  int parent2_pool =
      std::max(1, static_cast<int>(config_.parent2_top_ratio * pop_size));

  std::uniform_int_distribution<int> dist1(0, parent1_pool - 1);
  std::uniform_int_distribution<int> dist2(0, parent2_pool - 1);

  std::vector<Individual> next_gen;
  next_gen.reserve(pop_size);

  // Crossover
  while (static_cast<int>(next_gen.size()) < num_children) {
    int i1 = dist1(rng_);
    int i2 = dist2(rng_);
    const auto& p1 = population_[i1];
    const auto& p2 = population_[i2];

    auto chn = crossover(p1, p2);

    next_gen.push_back(std::move(chn.first));
    next_gen.push_back(std::move(chn.second));
  }

  // Random individuals
  for (int i = 0; i < num_random; i++) {
    Individual ind;
    ind.dna = random_dna(config_.dna_length, rng_, config_.max_triangle_radius);
    ind.fitness = 0.0f;
    next_gen.push_back(std::move(ind));
  }

  // Mutation
  for (auto& ind : next_gen) {
    mutate(ind);
  }

  population_ = std::move(next_gen);

  // Evaluate fitness
  fitness_.evaluate_population(population_, renderer_);

  // Sort by fitness descending
  std::sort(population_.begin(), population_.end(),
            [](const Individual& a, const Individual& b) {
              return a.fitness > b.fitness;
            });
}

std::pair<Individual, Individual> EvolutionEngine::crossover(
    const Individual& p1, const Individual& p2) {
  std::pair<Individual, Individual> res;

  res.first.dna.resize(config_.dna_length);
  res.second.dna.resize(config_.dna_length);

  res.first.fitness = 0.0f;
  res.second.fitness = 0.0f;

  std::uniform_int_distribution<int> coin(0, 1);

  for (int i = 0; i < config_.dna_length; i++) {
    bool c = coin(rng_);

    if (c) {
      res.first.dna[i] = p1.dna[i];
      res.second.dna[i] = p2.dna[i];
    } else {
      res.first.dna[i] = p2.dna[i];
      res.second.dna[i] = p1.dna[i];
    }
  }

  return res;
}

void EvolutionEngine::mutate(Individual& ind) {
  std::uniform_real_distribution<float> prob(0.0f, 1.0f);
  for (auto& gene : ind.dna) {
    if (prob(rng_) < config_.mutation_rate) {
      gene = random_gene(rng_, config_.max_triangle_radius);
    }
  }
}

const Individual& EvolutionEngine::best() const { return population_[0]; }

int EvolutionEngine::generation() const { return generation_; }

}  // namespace evo
