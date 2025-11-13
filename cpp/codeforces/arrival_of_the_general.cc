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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> solders(n);
  int max = 0, min = 0;
  for (int i = 0; i < n; ++i) {
    std::cin >> solders[i];
    if (solders[max] < solders[i]) max = i;
    if (solders[min] >= solders[i]) min = i;
  }
  std::cout << time_to_do(n - 1, max, min) << "\n";

  return 0;
}
