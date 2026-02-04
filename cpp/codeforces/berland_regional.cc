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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> university(n);
    for (auto& i : university) {
      std::cin >> i;
    }

    map<int, vector<int>> m;
    for (int i = 0; i < n; ++i) {
      int skill;
      std::cin >> skill;
      m[university[i]].push_back(skill);
    }

    vector<int64_t> res(n, 0);
    for (auto& p : m) {
      sort(p.second.begin(), p.second.end(), std::greater<int>());
      vector<int64_t> pref_sum(p.second.size() + 1);
      pref_sum.front() = 0;
      for (size_t idx = 0; idx < p.second.size(); ++idx) {
        pref_sum[idx + 1] = pref_sum[idx] + p.second[idx];
      }
      for (size_t k = 1; k <= p.second.size(); ++k) {
        auto last = p.second.size() - p.second.size() % k;
        res[k - 1] += pref_sum[last];
      }
    }

    for (auto r : res) {
      std::cout << r << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
