#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int time_to_do(int n, int max, int min) {
  return max + n - min - (max > min ? 1 : 0);
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> solders(n);
  int max_idx = 0, min_idx = 0;
  for (int i = 0; i < n; ++i) {
    std::cin >> solders[i];
    if (solders[max_idx] < solders[i]) max_idx = i;
    if (solders[min_idx] >= solders[i]) min_idx = i;
  }
  std::cout << time_to_do(n - 1, max_idx, min_idx) << "\n";

  return 0;
}
