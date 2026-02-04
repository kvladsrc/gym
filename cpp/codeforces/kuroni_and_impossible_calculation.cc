#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
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

int fast_power(int a, int power, int m) {
  if (power == 0) {
    return 1;
  }

  if (power == 1) {
    return a;
  }

  if (power % 2 == 0) {
    return fast_power((a * a) % m, power / 2, m);
  } else {
    auto res = fast_power(a, power - 1, m);
    res = (res * a) % m;
    return res;
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  int m = 0;
  std::cin >> n >> m;

  vector<int> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());

  int64_t res = 1;
  unordered_map<int, int> prev;

  for (size_t idx = 0; idx < n; ++idx) {
    int rem = a[idx] % m;

    for (auto p : prev) {
      auto dist = ((rem + m) - p.first) % m;
      res = (res * fast_power(dist, p.second, m)) % m;
    }

    prev[rem]++;
    if (res == 0) {
      break;
    }
  }

  std::cout << res << "\n";

  return 0;
}
