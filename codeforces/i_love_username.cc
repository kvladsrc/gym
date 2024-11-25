#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  int max, min, amazing = 0;
  std::cin >> max;
  min = max;

  for (int i = 1; i < n; ++i) {
    int points;
    std::cin >> points;
    if (points > max) {
      amazing++;
      max = points;
      continue;
    }

    if (points < min) {
      amazing++;
      min = points;
      continue;
    }
  }
  std::cout << amazing << "\n";

  return 0;
}
