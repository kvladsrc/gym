#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int seg_num(vector<int> &a, int el) {
  bool in_seg = false;
  int res = 0;
  for (auto i : a) {
    if (!in_seg && i != el) {
      in_seg = true;
    } else if (in_seg && i == el) {
      res++;
      in_seg = false;
    }
  }

  if (in_seg) {
    res++;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    unordered_map<int, int> m;
    for (auto &i : a) {
      std::cin >> i;
      m[i]++;
    }

    int less_freq = -1;
    for (auto p : m) {
      if (less_freq == -1) {
        less_freq = p.first;
      } else if (m[less_freq] > p.second) {
        less_freq = p.first;
      }
    }

    // We check most left, most right and less frequent elements. Idea
    // is that only leftmost or rightmost elements can be better than
    // most frequent;
    int best = INT_MAX;
    best = min(best, seg_num(a, a[0]));
    best = min(best, seg_num(a, a.back()));
    best = min(best, seg_num(a, less_freq));

    std::cout << best << "\n";
  }

  return 0;
}
