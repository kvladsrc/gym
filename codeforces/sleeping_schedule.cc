#include <algorithm>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  int h = 0;
  int l = 0;
  int r = 0;
  std::cin >> n >> h >> l >> r;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  map<int, int> options;

  if (l <= a.front() && a.front() <= r) {
    options[a.front()] = 1;
  } else {
    options[a.front()] = 0;
  }

  if (l <= (a.front() - 1) && (a.front() - 1) <= r) {
    options[a.front() - 1] = 1;
  } else {
    options[a.front() - 1] = 0;
  }

  for (size_t idx = 1; idx < n; ++idx) {
    map<int, int> next_options;

    for (auto p : options) {
      auto local_time = (p.first + a[idx]) % h;
      auto local_res = p.second;
      if (l <= local_time && local_time <= r) {
        local_res++;
      }
      next_options[local_time] = max(next_options[local_time], local_res);

      auto local_time_delay = (p.first + (a[idx] - 1)) % h;
      auto local_res_delay = p.second;
      if (l <= local_time_delay && local_time_delay <= r) {
        local_res_delay++;
      }
      next_options[local_time_delay] =
          max(next_options[local_time_delay], local_res_delay);
    }

    options = next_options;
  }

  auto res = 0;
  for (auto p : options) {
    res = max(res, p.second);
  }
  std::cout << res << "\n";

  return 0;
}
