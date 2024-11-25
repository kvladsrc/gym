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
    vector<bool> parity(n);

    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      parity[i] = a[i] % 2;
    }

    sort(a.begin(), a.end());
    bool res = true;
    for (int i = 0; i < n; ++i) {
      if ((a[i] % 2) != parity[i]) {
        res = false;
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
