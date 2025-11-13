#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  int total = 0, max = 0;
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    if (buf > max) max = buf;
    total += buf;
  }
  std::cout << max * n - total << "\n";

  return 0;
}
