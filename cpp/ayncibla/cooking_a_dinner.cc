#include <algorithm>
#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

struct Dish {
  int time_to_cook;
  int time_to_fresh;
};

static bool compareDishes(const Dish& a, const Dish& b) {
  if (a.time_to_cook == b.time_to_cook) {
    return a.time_to_fresh > b.time_to_fresh;
  }

  return a.time_to_cook > b.time_to_cook;
}

static bool cook_order(const vector<int>& time_to_cook,
                       const vector<int>& time_to_fresh) {
  int overall_cook_time = 0;
  std::vector<Dish> dishes(time_to_cook.size());
  for (size_t i = 0; i < time_to_cook.size(); ++i) {
    dishes[i].time_to_cook = time_to_cook[i];
    dishes[i].time_to_fresh = time_to_fresh[i];
    overall_cook_time += time_to_cook[i];
  }

  boost::range::sort(dishes, compareDishes);

  while (!dishes.empty()) {
    bool added = false;
    auto it = std::find_if(
        dishes.begin(), dishes.end(), [overall_cook_time](const Dish& d) {
          return d.time_to_fresh >= overall_cook_time - d.time_to_cook;
        });

    if (it != dishes.end()) {
      overall_cook_time -= it->time_to_cook;
      added = true;
      dishes.erase(it);
    }
    if (!added) {
      return false;
    }
  }

  return true;
}

int main() {
  size_t n = 0;
  std::cin >> n;
  vector<int> time_to_cook(n);
  vector<int> time_to_fresh(n);
  for (size_t i = 0; i < n; i++) {
    std::cin >> time_to_cook[i] >> time_to_fresh[i];
  }

  if (cook_order(time_to_cook, time_to_fresh)) {
    std::cout << "Yes"
              << "\n";
  } else {
    std::cout << "No"
              << "\n";
  }

  return 0;
}
