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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;
    map<int, int> m;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      m[buf]++;
    }

    int global_l = -1, global_r = -1;
    int local_l = -1, local_r = -1;
    for (auto p : m) {
      if (p.second < k) {
        continue;
      }

      if (local_r == p.first - 1) {
        local_r++;
      } else {
        local_l = local_r = p.first;
      }

      if (global_r - global_l <= local_r - local_l) {
        global_r = local_r;
        global_l = local_l;
      }
    }

    if (global_l != -1) {
      std::cout << global_l << " " << global_r << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
