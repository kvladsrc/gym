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
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    map<int, vector<int>> m;
    for (int i = 1; i <= n; ++i) {
      int buf = 0;
      std::cin >> buf;
      buf %= k;
      if (buf == 0) {
        buf = k;
      }
      m[buf].push_back(i);
    }

    for (auto it = m.rbegin(); it != m.rend(); ++it) {
      for (auto el : (*it).second) {
        std::cout << el << " ";
      }
    }
    std::cout << "\n";
  }

  return 0;
}
