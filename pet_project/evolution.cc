#include "pet_project/evolution.hpp"

#include <algorithm>
#include <cmath>
#include <deque>
#include <iostream>
#include <utility>

#include "pet_project/types.hpp"

namespace evo {

EvolutionEngine::EvolutionEngine(const Config& config, Renderer& renderer,
                                 Fitness& fitness)
    : config_(config),
      renderer_(renderer),
      fitness_(fitness),
      current_grid_size_(config.initial_grid_size),
      rng_(std::random_device{}()) {}

void EvolutionEngine::init_population() {
  int dna_len = current_grid_size_ * current_grid_size_;
  population_.resize(config_.population_size);
  for (auto& ind : population_) {
    ind.dna = random_dna(dna_len, rng_);
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
  generations_at_current_grid_ = 0;
  fitness_history_.clear();
}

void EvolutionEngine::step() {
  generation_++;
  generations_at_current_grid_++;

  // Track fitness for plateau detection using monotonic deque
  float current_fitness = population_[0].fitness;

  // Add to deque if it's increasing or equal to the last
  if (fitness_history_.empty() ||
      current_fitness >= fitness_history_.back().second) {
    fitness_history_.push_back({generations_at_current_grid_, current_fitness});
  }

  // Remove old entries outside the window
  int window = config_.stabilization_generations;
  while (!fitness_history_.empty() &&
         fitness_history_.front().first <
             generations_at_current_grid_ - window) {
    fitness_history_.pop_front();
  }

  // Check if we should refine the grid
  if (should_refine_grid()) {
    refine_grid();
  }

  int dna_len = current_grid_size_ * current_grid_size_;
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
    const auto& p1 = population_[dist1(rng_)];
    const auto& p2 = population_[dist2(rng_)];

    auto chn = crossover(p1, p2);

    next_gen.push_back(std::move(chn.first));
    next_gen.push_back(std::move(chn.second));
  }

  // Random individuals
  for (int i = 0; i < num_random; i++) {
    Individual ind;
    ind.dna = random_dna(dna_len, rng_);
    ind.fitness = 0.0f;
    next_gen.push_back(std::move(ind));
  }

  // Mutation
  for (auto& ind : next_gen) {
    mutate(ind);
  }

  // Elitism: always keep the best individual unchanged, makes fitnes
  // rate non-decreasing.
  next_gen.pop_back();
  next_gen.push_back(population_[0]);

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

  int dna_len = current_grid_size_ * current_grid_size_;
  res.first.dna.resize(dna_len);
  res.second.dna.resize(dna_len);

  res.first.fitness = 0.0f;
  res.second.fitness = 0.0f;

  std::uniform_int_distribution<int> coin(0, 1);

  for (int i = 0; i < dna_len; i++) {
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
      mutate_gene(gene, rng_);
    }
  }
}

const Individual& EvolutionEngine::best() const { return population_[0]; }

int EvolutionEngine::generation() const { return generation_; }

int EvolutionEngine::current_grid_size() const { return current_grid_size_; }

bool EvolutionEngine::should_refine_grid() const {
  // Don't refine if we've reached max grid size
  if (current_grid_size_ >= config_.max_grid_size) {
    return false;
  }

  // Need enough generations to check for plateau
  if (generations_at_current_grid_ < config_.stabilization_generations) {
    return false;
  }

  // Check if fitness has plateaued (improvement < threshold)
  // Deque contains monotonically non-decreasing fitness values
  if (fitness_history_.empty()) {
    return false;
  }

  float recent_start = fitness_history_.front().second;  // Oldest in window
  float recent_end = fitness_history_.back().second;  // Most recent (highest)
  float improvement = recent_end - recent_start;

  return improvement < config_.fitness_improvement_threshold;
}

void EvolutionEngine::refine_grid() {
  current_grid_size_ *= 2;
  generations_at_current_grid_ = 0;
  fitness_history_.clear();

  auto subdivide = config_.divide_interpolate ? subdivide_dna_interpolate
                                               : subdivide_dna;

  if (config_.divide_elite) {
    // Wipe out population with subdivided best individual
    auto best_dna = subdivide(population_[0].dna);
    for (auto& ind : population_) {
      ind.dna = best_dna;
      ind.fitness = 0.0f;
    }
  } else {
    // Subdivide each individual's DNA independently
    for (auto& ind : population_) {
      ind.dna = subdivide(ind.dna);
      ind.fitness = 0.0f;
    }
  }

  // Re-evaluate fitness with new resolution
  fitness_.evaluate_population(population_, renderer_);

  // Re-sort by fitness
  std::sort(population_.begin(), population_.end(),
            [](const Individual& a, const Individual& b) {
              return a.fitness > b.fitness;
            });

  std::cout << "\n=== Grid refined to " << current_grid_size_ << "x"
            << current_grid_size_ << " ===" << std::endl;
}

}  // namespace evo
