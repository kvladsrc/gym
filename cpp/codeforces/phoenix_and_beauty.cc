#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;
    vector<int> a(n);
    set<int> unique;
    for (auto &i : a) {
      std::cin >> i;
      unique.insert(i);
    }

    if (static_cast<int>(unique.size()) > k) {
      std::cout << -1 << "\n";
      continue;
    }

    vector<int> rep;
    std::copy(unique.begin(), unique.end(), std::back_inserter(rep));
    while (static_cast<int>(rep.size()) < k) {
      rep.push_back(rep[0]);
    }

    std::cout << n * k << "\n";
    for (int i = 0; i < n; ++i) {
      for (auto el : rep) {
        std::cout << el << " ";
      }
    }
    std::cout << "\n";
  }

  return 0;
}
