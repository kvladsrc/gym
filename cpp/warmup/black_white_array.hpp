#pragma once

#include <vector>

struct elem {
  int val{};
  bool deleted{};

  bool operator<(const elem& other) const { return val < other.val; }

  bool operator==(const elem& other) const { return val == other.val; }
};

class bwarray {
  std::vector<elem> black;
  std::vector<elem> white;
  std::size_t total{};

  // Lazy deletion.
  std::size_t total_lazy{};
  // Number of lazy deleted elements in segment.
  std::vector<int> lazy;

  std::size_t start_idx(int seg);
  std::size_t end_idx(int seg);
  bool segment_used(int seg);
  void merge(int seg);
  void fix(int seg);

 public:
  bwarray() : black(1) {}
  ~bwarray() = default;
  std::size_t size() { return total - total_lazy; }
  void insert(int el);
  bool search(int el);
  void remove(int el);
};
