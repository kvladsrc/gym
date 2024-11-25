#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }
    if (n == 1) {
      std::cout << (a[0] > 1 ? "NO" : "YES") << "\n";
      continue;
    }
    sort(a.begin(), a.end());
    if ((a[n - 1] - a[n - 2]) > 1) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
  }

  return 0;
}
