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

class CompareBuckets {
 public:
  bool operator()(const pair<int, int64_t> &p1,
                  const pair<int, int64_t> &p2) const {
    return p1.first > p2.first;
  }
};

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, m, x;
    std::cin >> n >> m >> x;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Combinatorics: m^n / n! overall combinations. Idea: Dijkstra
    // like.
    vector<int> buckets(n);
    vector<int64_t> bucket_sums(m);
    priority_queue<pair<int, int64_t>, vector<pair<int, int64_t>>,
                   CompareBuckets>
        heap;
    for (int i = 0; i < m; ++i) {
      heap.push({0, i});
      bucket_sums[i] = 0;
    }

    for (int i = 0; i < n; ++i) {
      auto b = heap.top();
      heap.pop();
      buckets[i] = b.second;
      bucket_sums[b.second] += a[i];
      heap.push({bucket_sums[b.second], b.second});
    }

    sort(bucket_sums.begin(), bucket_sums.end());
    bool res = true;
    if (abs(bucket_sums.back() - bucket_sums.front()) > x) {
      res = false;
    }

    if (res) {
      std::cout << "YES" << "\n";
      for (auto i : buckets) {
        std::cout << i + 1 << " ";
      }
      std::cout << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
