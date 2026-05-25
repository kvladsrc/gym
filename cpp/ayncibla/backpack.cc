#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

struct Normalized {
  double value = 0.0;
  int idx{};
};

static bool compareNormalized(const Normalized& a, const Normalized& b) {
  return a.value > b.value;
}

static double get_optimal_value(int capacity, const vector<int>& weights,
                                const vector<int>& values) {
  double value = 0.0;

  vector<Normalized> value_per_unit(weights.size());
  for (size_t i = 0; i < values.size(); ++i) {
    value_per_unit[i].value = static_cast<double>(values[i]) / weights[i];
    value_per_unit[i].idx = i;
  }

  boost::range::sort(value_per_unit, compareNormalized);

  for (Normalized const e : value_per_unit) {
    if (weights[e.idx] <= capacity) {
      value += values[e.idx];
      capacity -= weights[e.idx];
    } else {
      value += e.value * capacity;
      capacity = 0;
    }

    if (capacity == 0) {
      break;
    }
  }

  return value;
}

int main() {
  int n = 0;
  int capacity = 0;
  std::cin >> n >> capacity;
  vector<int> values(n);
  vector<int> weights(n);
  for (int i = 0; i < n; i++) {
    std::cin >> values[i] >> weights[i];
  }

  double const optimal_value = get_optimal_value(capacity, weights, values);

  std::cout.precision(10);
  std::cout << optimal_value << '\n';
  return 0;
}
