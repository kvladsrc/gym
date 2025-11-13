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
  vector<int> h(3);
  std::cin >> h[0] >> h[1] >> h[2];
  sort(h.begin(), h.end());
  int dist = h[1] - h[0] + h[2] - h[1];
  std::cout << dist << "\n";

  return 0;
}
