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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  map<int, vector<int>> m;
  for (int i = 1; i <= n; ++i) {
    int beauty = 0;
    std::cin >> beauty;
    m[beauty - i].push_back(beauty);
  }

  int64_t best = 0;
  for (auto p : m) {
    int64_t local = std::accumulate(p.second.begin(), p.second.end(),
                                    static_cast<int64_t>(0));
    best = max(local, best);
  }

  std::cout << best << "\n";

  return 0;
}
