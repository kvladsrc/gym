#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t) {
    int a, res = 0;
    std::cin >> a;
    for (int i = 0; i < 3; ++i) {
      int cur;
      std::cin >> cur;
      if (cur > a) res++;
    }
    std::cout << res << "\n";
    t--;
  }

  return 0;
}
