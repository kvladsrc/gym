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
  int n, k;
  std::cin >> n >> k;
  int teams = 0, buf = 0;
  ;
  for (int i = 0; i < n; ++i) {
    int s;
    std::cin >> s;
    if (s <= 5 - k) {
      teams += ++buf / 3;
      buf = buf % 3;
    }
  }
  std::cout << teams << "\n";

  return 0;
}
