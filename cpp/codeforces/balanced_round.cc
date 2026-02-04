#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<int> a(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
    }
    sort(a.begin(), a.end());

    int max_cons = 0, cur_cons = 1;
    for (int i = 1; i < n; ++i) {
      if (a[i] - a[i - 1] > k) {
        if (max_cons < cur_cons) max_cons = cur_cons;
        cur_cons = 1;
      } else {
        cur_cons++;
      }
    }
    if (max_cons < cur_cons) max_cons = cur_cons;

    std::cout << n - max_cons << "\n";
  }

  return 0;
}
