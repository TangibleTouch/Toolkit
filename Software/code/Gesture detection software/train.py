from os import listdir

import numpy as np
import pandas as pd

import tensorflow as tf
tf.get_logger().setLevel('ERROR')

import tensorflow.python.keras.backend as K

gestures = []
trainDir = '../gestures/'
files = listdir(trainDir)
for f in files:
    if '.csv' in f:
        gestures.append(f.split('.')[0])

print("Please note that Tensorflow 1.15.5 is depreceated.")

samples_per_gesture = 200
num_sensors = 12
num_gestures = len(gestures)
one_hot_encoded_gestures = np.eye(num_gestures)
inputs_train = []
outputs_train = []
inputs_tst = []
outputs_tst = []


# Load gestures from files
print("Loading data from files...")
for gesture_index in range(num_gestures):
    gesture = gestures[gesture_index]
    output = one_hot_encoded_gestures[gesture_index]
    df = pd.read_csv(trainDir + gesture + '.csv')
    num_recordings = int(df.shape[0] / samples_per_gesture)
    for i in range(num_recordings):
        tensor = []
        for j in range(samples_per_gesture):
            index = i * samples_per_gesture + j
            t1 = []

            # Replace with 0 if -1
            for a in range(num_sensors):
                if df[str('t' + str(a + 1))][index] == -1:
                    t1.append(0)
                else:
                    t1.append(df[str('t' + str(a + 1))][index])

            tensor += t1

        inputs_train.append(tensor)
        outputs_train.append(output)

print('Data loaded.')
inputs_train = np.array(inputs_train, dtype=float)
outputs_train = np.array(outputs_train, dtype=float)
shape_train = inputs_train.shape

# Replace column with 0 if over 40% are 1
inputs_train = inputs_train.reshape(
    (inputs_train.shape[0], num_sensors, samples_per_gesture), order='F')
for a in range(shape_train[0]):
    for x in range(num_sensors):
        if np.average(inputs_train[a][x]) >= 0.4:
            inputs_train[a][x] = np.zeros(samples_per_gesture)

inputs_train = np.transpose(inputs_train, [0, 2, 1])


# Shuffle data
num_inputs = len(inputs_train)
randomize = np.arange(num_inputs)
np.random.shuffle(randomize)
inputs_train = inputs_train[randomize]
outputs_train = outputs_train[randomize]

inputs_train = np.reshape(
    inputs_train,
    (inputs_train.shape[0],
     samples_per_gesture,
     num_sensors))

print('Data pre-processing complete.')
print(inputs_train.shape)


opt = tf.keras.optimizers.RMSprop(learning_rate=0.02)
loss = tf.keras.losses.CategoricalCrossentropy()

metrics = ['categorical_accuracy']

batch_size = 32
epochs = 60
decay = 0.08 / epochs

# Decaying learning rate
def learning_rate_decay(epoch, learning_rate):
    return learning_rate * 1 / (1 + decay * epoch)

model = tf.keras.Sequential()
# Sequence extraction
# make the sequences artificially longer and reduce timesteps by max + min pooling
# Remove empty timesteps with masking
model.add(tf.keras.layers.MaxPool1D(pool_size=3, strides=1))
model.add(tf.keras.layers.Lambda(
    lambda x: -tf.nn.max_pool1d((-x), ksize=5, strides=3, padding='VALID')))
#model.add(tf.keras.layers.Masking(mask_value=0.)) # Could not be used with TensorFlowSharp

# Add some noise to prevent overfitting
model.add(tf.keras.layers.GaussianNoise(0.1))
# Gated recurrent unit layer
model.add(tf.keras.layers.Bidirectional(tf.keras.layers.GRU(num_gestures * 3)))
# Output layer
model.add(tf.keras.layers.Dense(num_gestures, activation='softmax'))

model.compile(optimizer=opt, loss=loss, metrics=metrics)
history = model.fit(
    inputs_train,
    outputs_train,
    epochs=epochs,
    batch_size=batch_size,
    callbacks=[
        tf.keras.callbacks.LearningRateScheduler(
            learning_rate_decay)], verbose=1)

# A function to freeze session
def freeze_session(
        session,
        keep_var_names=None,
        output_names=None,
        clear_devices=True):
    from tensorflow.compat.v1.graph_util import convert_variables_to_constants
    graph = session.graph
    with graph.as_default():
        freeze_var_names = list(
            set((v.op.name for v in tf.compat.v1.global_variables())).difference(keep_var_names or []))
        output_names = output_names or []
        output_names += [v.op.name for v in tf.compat.v1.global_variables()]
        input_graph_def = graph.as_graph_def()
        if clear_devices:
            for node in input_graph_def.node:
                node.device = ''

        frozen_graph = convert_variables_to_constants(
            session, input_graph_def, output_names, freeze_var_names)
        return frozen_graph

# Freeze graph
frozen_graph = freeze_session(
    (K.get_session()), output_names=[
        out.op.name for out in model.outputs])
tf.io.write_graph(graph_or_graph_def=frozen_graph, logdir='../frozen_model',
                  name='frozen_graph.bytes',
                  as_text=False)

print('Finished!')
