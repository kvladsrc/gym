#include <algorithm>
#include <cctype>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool is_good(vector<int> const &a) {
  int sum = std::accumulate(a.begin(), a.end(), 0,
                            [](int sum, const auto i) { return sum + i; });

  if (sum % 2 != 0) {
    return true;
  }

  auto need = sum / 2;

  set<int> options;
  options.insert(0);
  auto rest = sum;

  for (auto i : a) {
    vector<int> to_add;
    to_add.push_back(i);
    rest -= i;

    for (auto o : options) {
      if (o + i == need) {
        return false;
      }

      if (o + i > need) {
        continue;
      }

      to_add.push_back(o + i);
    }

    for (auto ta : to_add) {
      options.insert(ta);
    }
  }

  return true;
}

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);

  int64_t sum = 0;
  for (auto &i : a) {
    std::cin >> i;
    sum += i;
  }

  if (sum % 2 != 0) {
    std::cout << 0 << "\n";
    return 0;
  }

  if (is_good(a)) {
    std::cout << 0 << "\n";
    return 0;
  }

  for (size_t idx = 0; idx < n; ++idx) {
    if (a[idx] % 2 != 0) {
      std::cout << 1 << "\n";
      std::cout << idx + 1 << "\n";
      return 0;
    }
  }

  int removed = a.back();
  a.pop_back();
  if (is_good(a)) {
    std::cout << 1 << "\n";
    std::cout << n << "\n";
    return 0;
  }

  for (size_t i = 0; i < a.size(); ++i) {
    std::swap(a[i], removed);
    if (is_good(a)) {
      std::cout << 1 << "\n";
      std::cout << i + 1 << "\n";
      return 0;
    }
  }

  return 0;
}
