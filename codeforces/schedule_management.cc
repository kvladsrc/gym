#include <algorithm>
#include <climits>
#include <cmath>
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

int64_t can_be_done(vector<int64_t> tasks, int64_t hours) {
  int64_t res = 0;
  for (auto i : tasks) {
    auto local = min(i, hours);
    if (local < hours) {
      local += (hours - local) / 2;
    }
    res += local;
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, m;
    std::cin >> n >> m;
    vector<int64_t> tasks(n, 0);
    int64_t total_tasks = 0;
    for (int64_t i = 0; i < m; ++i) {
      total_tasks++;
      int64_t buf;
      std::cin >> buf;
      tasks[buf - 1]++;
    }

    int64_t l = 0, r = total_tasks * 2;
    int64_t res = -1;
    while (l <= r) {
      int64_t mid = (l + r) / 2;
      if (can_be_done(tasks, mid) >= total_tasks) {
        res = mid;
        r = mid - 1;
      } else {
        l = mid + 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
