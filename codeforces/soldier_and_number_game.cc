#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <iterator>
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

const size_t constraints = 5000000;

void erato_fill(vector<int>& table) {
  for (size_t i = 2; i < table.size(); ++i) {
    if (table[i] == 0) {
      table[i]++;
      for (size_t j = 2; i * j < table.size(); ++j) {
        auto buf = i * j;
        while ((buf % i) == 0) {
          buf /= i;
          table[i * j]++;
        }
      }
    }
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  vector<int> erato(constraints + 1, 0);
  erato_fill(erato);
  vector<int64_t> pref_sum(constraints + 1);
  pref_sum.front() = 0;
  for (size_t i = 1; i <= constraints; ++i) {
    pref_sum[i] = pref_sum[i - 1] + erato[i];
  }

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    std::cout << pref_sum[a] - pref_sum[b] << "\n";
  }

  return 0;
}
